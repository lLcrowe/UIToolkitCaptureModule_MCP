using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace lLCroweTool.UIToolkitCapture
{
    /// <summary>
    /// UI Toolkit 캡처 Runtime 공개 API. PlayMode 작동 보장 영역.
    /// EditMode 강화 캡처는 Editor asmdef의 UIToolkitCaptureEditor 사용 (EditorApplication 의존).
    /// Claude MCP execute_code로 호출. AnimCaptureModule_MCP 패턴 정합.
    /// </summary>
    public static class UIToolkitCapture
    {
        /// <summary>
        /// UIDocument GO의 UI Toolkit panel을 PNG 캡처.
        /// PanelSettings.targetTexture에 RT 임시 할당 → 1프레임 강제 → RT 읽기 → PNG.
        /// PlayMode에서 안정 작동. EditMode는 best-effort (Editor 보강 권장).
        /// </summary>
        /// <param name="targetGameObjectName">Active Scene 안 UIDocument를 가진 GO 이름</param>
        /// <param name="width">캡처 가로</param>
        /// <param name="height">캡처 세로</param>
        /// <param name="outputPath">PNG 저장 경로 (예: "Assets/_Capture/test.png")</param>
        /// <returns>CaptureResult — success/outputPath/error</returns>
        public static CaptureResult CaptureUIDocument(string targetGameObjectName, int width, int height, string outputPath)
        {
            // 1. GO 찾기 (Active Scene 전체 탐색)
            var go = GameObject.Find(targetGameObjectName);
            if (go == null) return CaptureResult.Fail($"GameObject not found in active scene: {targetGameObjectName}");

            // 2. UIDocument 컴포넌트 가져오기
            var uiDoc = go.GetComponent<UIDocument>();
            if (uiDoc == null) return CaptureResult.Fail("UIDocument component not found on target");

            var panelSettings = uiDoc.panelSettings;
            if (panelSettings == null) return CaptureResult.Fail("UIDocument.panelSettings is null");

            return CaptureWithPanelSettings(panelSettings, uiDoc.rootVisualElement, width, height, outputPath);
        }

        /// <summary>
        /// PanelSettings + 임의 VisualElement root를 캡처. UIDocument 우회 호출용.
        /// </summary>
        public static CaptureResult CaptureWithPanelSettings(PanelSettings panelSettings, VisualElement rootElement, int width, int height, string outputPath)
        {
            if (panelSettings == null) return CaptureResult.Fail("PanelSettings is null");

            // 3. RT 생성 (32-bit ARGB)
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "UIToolkitCapture_RT" };
            rt.Create();

            var prevTargetTex = panelSettings.targetTexture;
            panelSettings.targetTexture = rt;

            try
            {
                // 4. panel dirty 마킹 — Repaint 트리거
                rootElement?.MarkDirtyRepaint();

                // 5. ⭐ 0.2.0 핵심 — RuntimePanel.Update() + UpdateForRepaint() + Render() reflection 직접 호출.
                //    EditMode/PlayMode 둘 다에서 panel을 _강제 렌더 사이클_ 통과시킴.
                //    Probe(`Tools/UI Toolkit Capture/Probe/Enum Panel Methods`)로 확인된 internal API.
                ForceRenderPanel(rootElement?.panel);

                // 6. RT → Texture2D
                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;

                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;

                // 7. PNG 저장
                EnsureDirectory(outputPath);
                var png = tex.EncodeToPNG();
                File.WriteAllBytes(outputPath, png);
                Object.DestroyImmediate(tex);

                return CaptureResult.Ok(outputPath, width, height);
            }
            catch (System.Exception e)
            {
                return CaptureResult.Fail($"Capture exception: {e.Message}");
            }
            finally
            {
                // 8. cleanup — RT 해제 + 원래 targetTexture 복원
                panelSettings.targetTexture = prevTargetTex;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        /// <summary>
        /// 0.2.0 핵심 — RuntimePanel.Update() + UpdateForRepaint() + Render() reflection 직접 호출.
        /// EditMode/PlayMode 둘 다에서 panel을 강제 렌더 사이클 통과시킴.
        /// Probe(`Tools/UI Toolkit Capture/Probe/Enum Panel Methods`)로 확인된 internal API.
        /// 0.1.0의 UIElementsRuntimeUtility.UpdatePanels은 효과 없어서 폐기.
        /// </summary>
        private static void ForceRenderPanel(IPanel panel)
        {
            if (panel == null) return;
            try
            {
                var t = panel.GetType();
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                // 1) Update — 모든 단계 통합 (ValidateLayout/ApplyStyles/UpdateBindings 등)
                var updateMethod = t.GetMethod("Update", flags, null, System.Type.EmptyTypes, null);
                updateMethod?.Invoke(panel, null);

                // 2) UpdateForRepaint — Repaint 직전 단계 (visualTreeUpdater에 Repaint 의존성 채움)
                var updateRepaintMethod = t.GetMethod("UpdateForRepaint", flags, null, System.Type.EmptyTypes, null);
                updateRepaintMethod?.Invoke(panel, null);

                // 3) Render — RT에 직접 그리기 (panelSettings.targetTexture 할당된 RT)
                var renderMethod = t.GetMethod("Render", flags, null, System.Type.EmptyTypes, null);
                renderMethod?.Invoke(panel, null);
            }
            catch
            {
                // reflection 실패 — Unity 버전 변경. 무시 (RT 할당만으로 PlayMode는 그래도 동작)
            }
        }

        private static void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
