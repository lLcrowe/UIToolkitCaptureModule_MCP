using UnityEditor;
using UnityEngine;
using lLCroweTool.UIToolkitCapture;

namespace lLCroweTool.UIToolkitCapture.Editor
{
    /// <summary>
    /// MenuItem 자동화 — 테스트·검증용. 선택 GameObject에 UIDocument가 있으면 캡처.
    /// </summary>
    public static class UIToolkitCaptureMenu
    {
        private const string OUTPUT_DIR = "Assets/_UIToolkitCapture";

        [MenuItem("Tools/UI Toolkit Capture/Capture Selected UIDocument (EditMode)")]
        public static void CaptureSelectedEditMode()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                Debug.LogWarning("[UIToolkitCapture] Hierarchy에서 UIDocument GO를 선택하세요.");
                return;
            }

            var path = $"{OUTPUT_DIR}/{go.name}_EditMode_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            var result = UIToolkitCaptureEditor.CaptureUIDocumentEditMode(go.name, 1920, 1080, path);
            LogResult(result, "EditMode");
        }

        [MenuItem("Tools/UI Toolkit Capture/Capture Selected UIDocument (Runtime PlayMode)")]
        public static void CaptureSelectedRuntime()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[UIToolkitCapture] Runtime 캡처는 PlayMode 필요. EditMode 메뉴를 사용하거나 Play 진입.");
                return;
            }

            var go = Selection.activeGameObject;
            if (go == null)
            {
                Debug.LogWarning("[UIToolkitCapture] Hierarchy에서 UIDocument GO를 선택하세요.");
                return;
            }

            var path = $"{OUTPUT_DIR}/{go.name}_Runtime_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            var result = UIToolkitCapture.CaptureUIDocument(go.name, 1920, 1080, path);
            LogResult(result, "Runtime");
        }

        [MenuItem("Tools/UI Toolkit Capture/Test/EditMode UIDocumentTest")]
        public static void TestEditModeUIDocumentTest()
        {
            var path = $"{OUTPUT_DIR}/Test_EditMode_UIDocumentTest_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            var result = UIToolkitCaptureEditor.CaptureUIDocumentEditMode("UIDocumentTest", 1920, 1080, path);
            LogResult(result, "Test_EditMode");
        }

        [MenuItem("Tools/UI Toolkit Capture/Capture EditorWindow (실험)")]
        public static void CaptureEditorWindowMenu()
        {
            // 예제 — SceneView 캡처 시도
            var path = $"{OUTPUT_DIR}/EditorWindow_SceneView_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            var result = UIToolkitCaptureEditor.CaptureEditorWindow("UnityEditor.SceneView", 1280, 720, path);
            LogResult(result, "EditorWindow(SceneView)");
        }

        private static void LogResult(CaptureResult result, string mode)
        {
            if (result.success)
                Debug.Log($"[UIToolkitCapture/{mode}] 성공 → {result.outputPath} ({result.width}x{result.height})");
            else
                Debug.LogError($"[UIToolkitCapture/{mode}] 실패 → {result.error}");
        }
    }
}
