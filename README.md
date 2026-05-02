# UI Toolkit Capture Module (MCP)

Unity UI Toolkit panel·EditorWindow 캡처 모듈. AI/MCP 워크플로우 전용. AnimCaptureModule_MCP 패턴 정합.

## 목적

UnityMCP 기본 `manage_ui render_ui` 도구는 EditMode에서 best-effort (검정 화면 발생). 본 모듈은 _EditMode 강화 캡처_ + _EditorWindow 캡처_ 보강.

- 사용자: "이 UI 어떤지 봐줘" (Play 진입 부담 없이 EditMode에서)
- Claude: MCP `execute_code`로 `UIToolkitCapture.CaptureUIDocument(...)` 호출 → PNG 생성 → `Read`로 분석

## 핵심 API

### Runtime (PlayMode 작동 보장 + EditMode best-effort)

```csharp
using lLCroweTool.UIToolkitCapture;

CaptureResult result = UIToolkitCapture.CaptureUIDocument(
    targetGameObjectName: "UIDocumentTest",
    width: 1920,
    height: 1080,
    outputPath: "Assets/_UIToolkitCapture/test.png"
);
```

### Editor (EditMode 강화 + EditorWindow)

```csharp
using lLCroweTool.UIToolkitCapture.Editor;

// EditMode 강화 — EditorApplication.QueuePlayerLoopUpdate + UpdatePanels reflection
CaptureResult r1 = UIToolkitCaptureEditor.CaptureUIDocumentEditMode(
    "UIDocumentTest", 1920, 1080,
    "Assets/_UIToolkitCapture/editmode.png");

// EditorWindow rootVisualElement 캡처 (실험 0.1.0 — 미작동)
CaptureResult r2 = UIToolkitCaptureEditor.CaptureEditorWindow(
    "UnityEditor.SceneView", 1280, 720,
    "Assets/_UIToolkitCapture/sceneview.png");
```

## 동작 방식

### CaptureUIDocument (Runtime)

1. **GO 찾기** — `GameObject.Find(targetName)` (Active Scene)
2. **UIDocument + PanelSettings 추출**
3. **RenderTexture 생성** (ARGB32) + `panelSettings.targetTexture` 임시 할당
4. **Repaint 트리거** — `rootElement.MarkDirtyRepaint()` + `UIElementsRuntimeUtility.UpdatePanels()` reflection
5. **RT → Texture2D → PNG 저장**
6. **cleanup** — `targetTexture` 복원 + RT.Release + DestroyImmediate

### CaptureUIDocumentEditMode (Editor)

Runtime 패턴 + 추가:
- `EditorApplication.QueuePlayerLoopUpdate()` 강제 frame loop
- `SceneView.RepaintAll()` 보조 트리거
- `AssetDatabase.Refresh()` PNG 인식

## MenuItem (테스트·검증용)

`Tools > UI Toolkit Capture` 메뉴:
- **Capture Selected UIDocument (EditMode)** — Hierarchy에서 UIDocument GO 선택 후 호출
- **Capture Selected UIDocument (Runtime PlayMode)** — Play 진입 후 호출
- **Capture EditorWindow (실험)** — SceneView 캡처 시도 (0.1.0 미작동)

## 한계 (0.2.0 잔존)

- **EditorWindow 캡처 미구현** — EditorWindow 자체 panel은 internal IPanel 접근 별도 경로 필요. 0.3.0 후속
- **Active Scene 단일 탐색** — Loaded scene 다중 시 정확한 GO 식별 위해 Hierarchy 전체 경로 입력 옵션 필요 (후속)
- **internal API 의존** — `RuntimePanel.Update/UpdateForRepaint/Render` 메서드 시그니처가 Unity 6.3.12 기준. 버전 업그레이드 시 Probe로 재검증 필요

## 차별점 (vs UnityMCP manage_ui render_ui)

| 영역 | manage_ui | UIToolkitCapture |
|------|:--------:|:----------------:|
| PlayMode 캡처 | ✅ (2번 호출) | ✅ (1번 호출) |
| EditMode 캡처 | ❌ blank | ✅ **0.2.0 작동** (RuntimePanel.Render reflection) |
| EditorWindow 캡처 | ❌ | ⏸ 0.3.0 후속 |
| 호출 방식 | MCP tool | Runtime API (`execute_code`) 또는 MenuItem (`execute_menu_item`) |
| UnityMCP 의존 | 필수 | 독립 (MCP 서버 fork 부담 0) |

## 후속 (0.2.0 후보)

- EditorWindow 캡처 본격 구현 (panel internal API reflection 정착)
- 멀티 씬 GO 경로 매칭 (Hierarchy 전체 경로 옵션)
- 자동 ForcePolling (PlayMode 1번 호출로 끝)
- Texture format 옵션 (HDR/sRGB)
- Async 캡처 (대형 UI panel)
