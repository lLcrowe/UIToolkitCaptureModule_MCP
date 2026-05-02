using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace lLCroweTool.UIToolkitCapture.Editor
{
    /// <summary>
    /// 0.2.0 진단용 — UIDocument.panel internal API enum + 로그 출력.
    /// Selection에 UIDocument GO 두고 Probe 메뉴 호출 → Console에서 사용 가능 메서드 식별.
    /// </summary>
    public static class UIToolkitCaptureProbe
    {
        private const string TARGET_NAME = "UIDocumentTest";
        private const string OUTPUT_DIR = "Assets/_UIToolkitCapture/Probe";

        private static void WriteAndLog(string fileName, string content)
        {
            if (!Directory.Exists(OUTPUT_DIR)) Directory.CreateDirectory(OUTPUT_DIR);
            var path = $"{OUTPUT_DIR}/{fileName}";
            File.WriteAllText(path, content);
            AssetDatabase.Refresh();
            Debug.Log($"[Probe] saved → {path} ({content.Length} chars)");
        }

        [MenuItem("Tools/UI Toolkit Capture/Probe/Enum Panel Methods")]
        public static void ProbePanelMethods()
        {
            var go = GameObject.Find(TARGET_NAME);
            if (go == null) { Debug.LogError($"[Probe] {TARGET_NAME} not found"); return; }

            var uiDoc = go.GetComponent<UIDocument>();
            if (uiDoc == null) { Debug.LogError("[Probe] UIDocument missing"); return; }

            var root = uiDoc.rootVisualElement;
            if (root == null) { Debug.LogError("[Probe] rootVisualElement null"); return; }

            var panel = root.panel;
            if (panel == null) { Debug.LogError("[Probe] panel null"); return; }

            var sb = new StringBuilder();
            sb.AppendLine($"=== Panel Probe ===");
            sb.AppendLine($"Panel type: {panel.GetType().FullName}");
            sb.AppendLine($"Panel assembly: {panel.GetType().Assembly.GetName().Name}");
            sb.AppendLine();
            sb.AppendLine("--- Methods (Public + NonPublic Instance) ---");

            var methods = panel.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var m in methods)
            {
                if (m.GetParameters().Length > 1) continue; // 0~1 인자만
                var paramStr = m.GetParameters().Length == 0 ? "" : m.GetParameters()[0].ParameterType.Name;
                sb.AppendLine($"  {m.ReturnType.Name} {m.Name}({paramStr})");
            }

            WriteAndLog($"panel_methods_{DateTime.Now:yyyyMMdd_HHmmss}.txt", sb.ToString());
        }

        [MenuItem("Tools/UI Toolkit Capture/Probe/Enum UIElementsRuntimeUtility Methods")]
        public static void ProbeRuntimeUtility()
        {
            var asm = typeof(UIDocument).Assembly;
            var t = asm.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
            if (t == null) { Debug.LogError("[Probe] UIElementsRuntimeUtility type not found"); return; }

            var sb = new StringBuilder();
            sb.AppendLine($"=== UIElementsRuntimeUtility ===");
            sb.AppendLine($"Assembly: {asm.GetName().Name}");
            sb.AppendLine();
            sb.AppendLine("--- Static Methods ---");
            var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var m in methods)
            {
                var ps = m.GetParameters();
                if (ps.Length > 1) continue;
                var pStr = ps.Length == 0 ? "" : ps[0].ParameterType.Name;
                sb.AppendLine($"  {m.ReturnType.Name} {m.Name}({pStr})");
            }

            WriteAndLog($"panel_methods_{DateTime.Now:yyyyMMdd_HHmmss}.txt", sb.ToString());
        }

        [MenuItem("Tools/UI Toolkit Capture/Probe/Enum EditorWindow Panel (SceneView)")]
        public static void ProbeEditorWindowPanel()
        {
            var window = EditorWindow.GetWindow<SceneView>();
            if (window == null) { Debug.LogError("[Probe] SceneView not found"); return; }

            var root = window.rootVisualElement;
            var panel = root?.panel;
            if (panel == null) { Debug.LogError("[Probe] EditorWindow panel null"); return; }

            var sb = new StringBuilder();
            sb.AppendLine($"=== EditorWindow Panel Probe ===");
            sb.AppendLine($"Window type: {window.GetType().FullName}");
            sb.AppendLine($"Window position: {window.position}");
            sb.AppendLine($"Panel type: {panel.GetType().FullName}");
            sb.AppendLine($"Panel base: {panel.GetType().BaseType?.FullName}");
            sb.AppendLine($"Panel assembly: {panel.GetType().Assembly.GetName().Name}");
            sb.AppendLine();
            sb.AppendLine("--- Methods (Public + NonPublic Instance) ---");

            var methods = panel.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var m in methods)
            {
                if (m.GetParameters().Length > 1) continue;
                var paramStr = m.GetParameters().Length == 0 ? "" : m.GetParameters()[0].ParameterType.Name;
                sb.AppendLine($"  {m.ReturnType.Name} {m.Name}({paramStr})");
            }

            sb.AppendLine();
            sb.AppendLine("--- Properties (Public + NonPublic Instance) ---");
            var props = panel.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var p in props)
            {
                sb.AppendLine($"  {p.PropertyType.Name} {p.Name}");
            }

            WriteAndLog($"editorwindow_panel_methods_{DateTime.Now:yyyyMMdd_HHmmss}.txt", sb.ToString());
        }

        [MenuItem("Tools/UI Toolkit Capture/Probe/Coords (SceneView + Inspector)")]
        public static void ProbeCoords()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Coords Probe ===");
            sb.AppendLine($"DateTime: {DateTime.Now}");
            sb.AppendLine();

            ProbeWindowCoords(sb, "SceneView", typeof(SceneView));
            sb.AppendLine();

            try
            {
                var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
                if (inspectorType != null) ProbeWindowCoords(sb, "InspectorWindow", inspectorType);
            }
            catch (Exception e) { sb.AppendLine($"Inspector probe failed: {e.Message}"); }

            sb.AppendLine();
            sb.AppendLine("=== Process Info ===");
            try
            {
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                sb.AppendLine($"Process MainWindowHandle: {proc.MainWindowHandle}");
                sb.AppendLine($"Process MainWindowTitle: {proc.MainWindowTitle}");
            }
            catch (Exception e) { sb.AppendLine($"Process info failed: {e.Message}"); }

            WriteAndLog($"coords_{DateTime.Now:yyyyMMdd_HHmmss}.txt", sb.ToString());
        }

        private static void ProbeWindowCoords(StringBuilder sb, string label, Type windowType)
        {
            try
            {
                var window = EditorWindow.GetWindow(windowType) as EditorWindow;
                if (window == null) { sb.AppendLine($"--- {label}: GetWindow null"); return; }

                var hwnd = OSScreenCapture.GetEditorWindowHWND(window);
                var rect = OSScreenCapture.GetEditorWindowRect(window);

                sb.AppendLine($"--- {label} ({windowType.FullName}) ---");
                sb.AppendLine($"  window.position: {window.position}");
                sb.AppendLine($"  HWND (reflection): {hwnd}");
                sb.AppendLine($"  GetWindowRect: ({rect.left}, {rect.top}) - ({rect.right}, {rect.bottom}) = {rect.Width}x{rect.Height}");
                sb.AppendLine($"  Calculated abs: ({rect.left + window.position.x}, {rect.top + window.position.y}) {window.position.width}x{window.position.height}");

                // m_Parent reflection 직접 확인
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var parentField = typeof(EditorWindow).GetField("m_Parent", flags);
                var parent = parentField?.GetValue(window);
                if (parent != null)
                {
                    sb.AppendLine($"  m_Parent type: {parent.GetType().FullName}");
                    var windowField = parent.GetType().GetField("window", flags);
                    var container = windowField?.GetValue(parent);
                    if (container != null)
                    {
                        sb.AppendLine($"  ContainerWindow type: {container.GetType().FullName}");
                        var posField = container.GetType().GetField("m_PixelRect", flags);
                        if (posField != null)
                        {
                            var pixelRect = posField.GetValue(container);
                            sb.AppendLine($"  ContainerWindow.m_PixelRect: {pixelRect}");
                        }
                        var posProp = container.GetType().GetProperty("position", flags);
                        if (posProp != null)
                        {
                            var pos = posProp.GetValue(container);
                            sb.AppendLine($"  ContainerWindow.position: {pos}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                sb.AppendLine($"--- {label}: ERROR {e.Message}");
            }
        }

        [MenuItem("Tools/UI Toolkit Capture/Probe/Try Each Repaint Method")]
        public static void TryEachRepaint()
        {
            var go = GameObject.Find(TARGET_NAME);
            if (go == null) { Debug.LogError($"[Probe] {TARGET_NAME} not found"); return; }

            var uiDoc = go.GetComponent<UIDocument>();
            var root = uiDoc?.rootVisualElement;
            var panel = root?.panel;
            if (panel == null) { Debug.LogError("[Probe] panel null"); return; }

            // 후보 메서드 호출 시도
            var candidates = new[] {
                "Repaint",
                "UpdateAnimations",
                "UpdateForRepaint",
                "UpdateScheduledEvents",
                "ValidateLayout",
                "UpdateBindings",
                "UpdateAssetTrackers",
                "Update"
            };

            foreach (var name in candidates)
            {
                var method = panel.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, Type.EmptyTypes, null);
                if (method != null)
                {
                    try
                    {
                        method.Invoke(panel, null);
                        Debug.Log($"[Probe] {name}() OK");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Probe] {name}() throw: {e.InnerException?.Message ?? e.Message}");
                    }
                }
                else
                {
                    Debug.Log($"[Probe] {name}() NOT FOUND on {panel.GetType().Name}");
                }
            }
        }
    }
}
