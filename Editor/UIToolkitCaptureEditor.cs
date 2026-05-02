using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace lLCroweTool.UIToolkitCapture.Editor
{
    /// <summary>
    /// UI Toolkit 캡처 Editor 강화 API.
    /// EditMode 캡처(EditorApplication 강제 frame) + EditorWindow rootVisualElement 캡처.
    /// Runtime UIToolkitCapture와 짝 (Runtime은 PlayMode 작동 보장, Editor는 EditMode + EditorWindow).
    /// </summary>
    public static class UIToolkitCaptureEditor
    {
        /// <summary>
        /// EditMode 강화 캡처. PanelSettings.targetTexture 할당 + EditorApplication.QueuePlayerLoopUpdate +
        /// internal API UpdatePanels reflection 강제 호출 → RT 읽기 → PNG.
        /// 0.1.0 실험 단계 — Unity 6.3에서 작동 여부 검증 필요.
        /// </summary>
        public static CaptureResult CaptureUIDocumentEditMode(string targetGameObjectName, int width, int height, string outputPath)
        {
            // 1. GO 찾기
            var go = GameObject.Find(targetGameObjectName);
            if (go == null) return CaptureResult.Fail($"GameObject not found: {targetGameObjectName}");

            var uiDoc = go.GetComponent<UIDocument>();
            if (uiDoc == null) return CaptureResult.Fail("UIDocument component missing");

            var panelSettings = uiDoc.panelSettings;
            if (panelSettings == null) return CaptureResult.Fail("PanelSettings unassigned");

            return CaptureUIDocumentEditMode(panelSettings, uiDoc.rootVisualElement, width, height, outputPath);
        }

        /// <summary>
        /// PanelSettings + VisualElement root 직접 EditMode 캡처. UIDocument 우회.
        /// </summary>
        public static CaptureResult CaptureUIDocumentEditMode(PanelSettings panelSettings, VisualElement rootElement, int width, int height, string outputPath)
        {
            if (panelSettings == null) return CaptureResult.Fail("PanelSettings null");

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "UIToolkitCaptureEditor_RT" };
            rt.Create();

            var prevTargetTex = panelSettings.targetTexture;
            panelSettings.targetTexture = rt;

            try
            {
                // 1) panel dirty 마킹
                rootElement?.MarkDirtyRepaint();

                // 2) ⭐ 0.2.0 핵심 — RuntimePanel.Update() + UpdateForRepaint() + Render() reflection 직접 호출.
                //    0.1.0의 UpdatePanels() reflection은 효과 없었음 (검정 화면).
                //    Probe 결과로 확인된 RuntimePanel 메서드 (UnityEngine.UIElementsModule):
                //    - Update() : 모든 단계 통합 (ValidateLayout/ApplyStyles/UpdateBindings 등)
                //    - UpdateForRepaint() : Repaint 직전 단계
                //    - Render() : RT에 직접 그리기
                ForceRenderPanel(rootElement?.panel);

                // 3) Editor scene view 강제 repaint — 보조 (HUD overlay 등 갱신용)
                SceneView.RepaintAll();
                EditorApplication.QueuePlayerLoopUpdate();

                // 4) RT → Texture2D
                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;

                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;

                // 6) PNG 저장
                EnsureDirectory(outputPath);
                var png = tex.EncodeToPNG();
                File.WriteAllBytes(outputPath, png);
                UnityEngine.Object.DestroyImmediate(tex);

                AssetDatabase.Refresh();
                return CaptureResult.Ok(outputPath, width, height);
            }
            catch (Exception e)
            {
                return CaptureResult.Fail($"EditMode capture exception: {e.Message}");
            }
            finally
            {
                panelSettings.targetTexture = prevTargetTex;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        /// <summary>
        /// EditorWindow의 rootVisualElement를 캡처. 임시 RenderTexture 할당 패턴.
        /// 실험 0.1.0 — Unity 6.3에서 EditorWindow panel 캡처 작동 여부 검증 필요.
        /// </summary>
        /// <param name="editorWindowTypeName">예: "UnityEditor.SceneView" 또는 "MyTool.IAMEditorWindow"</param>
        public static CaptureResult CaptureEditorWindow(string editorWindowTypeName, int width, int height, string outputPath)
        {
            if (string.IsNullOrEmpty(editorWindowTypeName)) return CaptureResult.Fail("editorWindowTypeName empty");

            // 1) Type 찾기 — 모든 로드된 assembly 순회
            Type targetType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(editorWindowTypeName);
                if (t != null) { targetType = t; break; }
            }
            if (targetType == null) return CaptureResult.Fail($"Type not found: {editorWindowTypeName}");
            if (!typeof(EditorWindow).IsAssignableFrom(targetType)) return CaptureResult.Fail($"Type is not EditorWindow: {editorWindowTypeName}");

            // 2) GetWindow (이미 열려있으면 그것, 아니면 신규)
            var window = EditorWindow.GetWindow(targetType) as EditorWindow;
            if (window == null) return CaptureResult.Fail("Failed to GetWindow");

            try
            {
                window.minSize = new Vector2(width, height);
                window.maxSize = new Vector2(width, height);
                window.Repaint();

                // EditorWindow.rootVisualElement.panel을 캡처는 internal API 의존 영역.
                // 0.1.0에서는 _OS-level 캡처_ 대신 _Game view 패턴_ 시도.
                // window의 PanelSettings 추출 시도 (private field reflection)
                var panel = window.rootVisualElement?.panel;
                if (panel == null) return CaptureResult.Fail("EditorWindow panel null");

                // EditorWindow의 panel은 internal Panel — render to RT가 internal API 의존.
                // 시도: panel.SendForceRedraw() reflection
                ForceUpdatePanels();
                window.Repaint();
                EditorApplication.QueuePlayerLoopUpdate();

                // 임시 응답 — 실측 후 internal API 정착 시 본 메서드 보강
                return CaptureResult.Fail("EditorWindow capture not yet implemented in 0.1.0 — internal Panel API access pending. Use Unity 'Window > Capture' or external tool for now.");
            }
            catch (Exception e)
            {
                return CaptureResult.Fail($"EditorWindow capture exception: {e.Message}");
            }
        }

        /// <summary>
        /// 0.2.0 핵심 — RuntimePanel.Update() + UpdateForRepaint() + Render() reflection 직접 호출.
        /// EditMode에서 frame loop 없어도 panel을 강제 렌더 사이클 통과시킴.
        /// Probe 결과(`Tools/UI Toolkit Capture/Probe/Enum Panel Methods`)로 확인된 메서드 시그니처 의존.
        /// </summary>
        private static void ForceRenderPanel(IPanel panel)
        {
            if (panel == null) return;
            try
            {
                var t = panel.GetType();
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                // 1) Update — 모든 단계 통합 (ValidateLayout/ApplyStyles/UpdateBindings 등 자동 진행)
                var updateMethod = t.GetMethod("Update", flags, null, System.Type.EmptyTypes, null);
                updateMethod?.Invoke(panel, null);

                // 2) UpdateForRepaint — Repaint 직전 단계 (visualTreeUpdater에 Repaint 의존성 채움)
                var updateRepaintMethod = t.GetMethod("UpdateForRepaint", flags, null, System.Type.EmptyTypes, null);
                updateRepaintMethod?.Invoke(panel, null);

                // 3) Render — RT에 직접 그리기 (panelSettings.targetTexture 할당된 RT)
                var renderMethod = t.GetMethod("Render", flags, null, System.Type.EmptyTypes, null);
                renderMethod?.Invoke(panel, null);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[UIToolkitCaptureEditor] ForceRenderPanel reflection 실패: {e.InnerException?.Message ?? e.Message}");
            }
        }

        /// <summary>
        /// 0.1.0 폴백 (UpdatePanels 호출). 0.2.0에서 ForceRenderPanel 사용으로 대체됨.
        /// 호환성 위해 EditorWindow 메서드에서만 사용.
        /// </summary>
        private static void ForceUpdatePanels()
        {
            try
            {
                var asm = typeof(UIDocument).Assembly;
                var utilType = asm.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
                if (utilType == null) return;
                var method = utilType.GetMethod(
                    "UpdatePanels",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                method?.Invoke(null, null);
            }
            catch { /* reflection 실패 무시 */ }
        }

        private static void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
