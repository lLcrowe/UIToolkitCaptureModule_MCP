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
        /// EditorWindow의 가시 영역을 OS-level BitBlt로 캡처해 PNG 저장.
        /// 0.3.0 — EditorPanel은 RT 할당 경로 없어서 OS 화면 캡처 + crop 패턴 채택.
        /// Windows 전용 (P/Invoke user32/gdi32). 다른 플랫폼은 추후 분기.
        /// </summary>
        /// <param name="editorWindowTypeName">예: "UnityEditor.SceneView" 또는 "MyTool.IAMEditorWindow"</param>
        /// <param name="outputPath">PNG 저장 경로</param>
        public static CaptureResult CaptureEditorWindow(string editorWindowTypeName, string outputPath)
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

            // 3) 강제 Repaint — UI 갱신
            window.Focus();
            window.Repaint();
            EditorApplication.QueuePlayerLoopUpdate();

            // 0.5.0 — Unity 메인 ContainerWindow PrintWindow 통째 캡처 + EditorWindow 영역 crop.
            //   1) FindUnityEditorWindow → Unity 메인 HWND
            //   2) PrintWindow → 통째 Texture (border 포함, ContainerWindow 좌상단 (0,0) 기준)
            //   3) window.position(절대 데스크톱 좌표) - ContainerWindow.GetWindowRect.left/top(절대) = ContainerWindow 안 상대 좌표
            //   4) crop → 단독 EditorWindow PNG

            var unityHwnd = OSScreenCapture.FindUnityEditorWindow();
            if (unityHwnd == IntPtr.Zero) return CaptureResult.Fail("Unity Editor 메인 ContainerWindow HWND 미발견");

            var containerRect = OSScreenCapture.FindAndGetUnityEditorRect();
            var pos = window.position;

            // ContainerWindow 안 상대 좌표 (PrintWindow 결과 텍스처에서 crop 위치)
            int relX = Mathf.RoundToInt(pos.x) - containerRect.left;
            int relY = Mathf.RoundToInt(pos.y) - containerRect.top;
            int w = Mathf.RoundToInt(pos.width);
            int h = Mathf.RoundToInt(pos.height);

            if (w <= 0 || h <= 0) return CaptureResult.Fail($"Invalid window size: {w}x{h}");

            // 5) PrintWindow 통째 캡처 + crop
            Texture2D fullTex = null;
            Texture2D croppedTex = null;
            try
            {
                fullTex = OSScreenCapture.CaptureWindowPrint(unityHwnd);
                if (fullTex == null) return CaptureResult.Fail("PrintWindow Unity 메인 캡처 실패");

                croppedTex = OSScreenCapture.CropTexture(fullTex, relX, relY, w, h);
                if (croppedTex == null) return CaptureResult.Fail($"Crop 실패 — relX={relX}, relY={relY}, w={w}, h={h}, fullTex={fullTex.width}x{fullTex.height}");

                EnsureDirectory(outputPath);
                File.WriteAllBytes(outputPath, croppedTex.EncodeToPNG());

                AssetDatabase.Refresh();
                return CaptureResult.Ok(outputPath, croppedTex.width, croppedTex.height);
            }
            catch (Exception e)
            {
                return CaptureResult.Fail($"EditorWindow capture exception: {e.Message}");
            }
            finally
            {
                if (croppedTex != null) UnityEngine.Object.DestroyImmediate(croppedTex);
                if (fullTex != null) UnityEngine.Object.DestroyImmediate(fullTex);
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
