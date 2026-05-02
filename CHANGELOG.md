# Changelog

## [0.4.0] - 2026-05-02 ⭐ Unity Editor 메인 캡처 작동

### Added
- **PrintWindow API** + **EnumWindows + 클래스명 매칭** — Unity Editor 메인 ContainerWindow HWND 정확 식별
- `OSScreenCapture.FindUnityEditorWindow()` — 현재 프로세스 내 _Unity_ 클래스명 가시 윈도우 중 가장 큰 영역 반환
- `OSScreenCapture.CaptureWindowPrint(hwnd)` — PrintWindow PW_RENDERFULLCONTENT, BitBlt fallback
- `Tools > UI Toolkit Capture > Capture Unity Editor Main (PrintWindow)` MenuItem

### Verified
- Unity 6.3.12 검증: Unity Editor 통째 캡처 정상 (Game view + Scene view + Hierarchy + Inspector + 콘솔 등 전부)
- 0.3.0의 _다른 윈도우 겹침_ 문제 해결 — PrintWindow는 _윈도우 자체 내용_ 캡처라 다른 앱 무시
- `m_WindowPtr` reflection은 OS HWND 아님 확인 → `EnumWindows`로 우회 정착

### Known Limitations (0.4.0 잔존)
- **단일 EditorWindow 영역 crop 미구현** — Unity 메인 통째 캡처만. SceneView/Inspector 단독 영역 추출은 0.5.0 후속 (`window.position`은 절대 좌표지만 PrintWindow 결과는 ContainerWindow 좌상단 (0,0) 기준이라 변환 필요)
- 다중 ContainerWindow (floating EditorWindow) 환경에서 _가장 큰 영역_만 반환 — 특정 floating 윈도우 캡처는 추후
- Windows 전용 — macOS/Linux는 별도 분기 필요

## [0.3.0] - 2026-05-02 (실험적, 좌표 보정 미완료)

### Added
- **EditorWindow OS-level 캡처 시도** — P/Invoke BitBlt (gdi32) + ContainerWindow HWND reflection chain
- `OSScreenCapture` Editor 클래스 — `CaptureScreenRegion(x, y, w, h)` + `GetEditorWindowHWND` + `GetEditorWindowRect`
- `Tools > UI Toolkit Capture > Capture EditorWindow (SceneView)` MenuItem
- `Tools > UI Toolkit Capture > Capture EditorWindow (Inspector)` MenuItem
- `Tools > UI Toolkit Capture > Probe > Coords (SceneView + Inspector)` 진단 메뉴
- `Tools > UI Toolkit Capture > Probe > Enum EditorWindow Panel (SceneView)` 진단 메뉴
- EditorPanel API 연구 결과 (Render/UpdateForRepaint/UpdateAnimations 메서드 확인 — 단 RT 할당 경로 없음)

### Verified
- BitBlt 자체는 작동 (PNG 정상 생성, RGB 변환 정합)
- `EditorWindow.position`은 절대 데스크톱 좌표 (Inspector (1517, 87) 검증)
- `EditorWindow.m_Parent` = DockArea / `DockArea.window` reflection 가능

### Known Limitations (0.3.0)
- ⚠️ **좌표 위에 다른 윈도우 겹칠 시 잘못된 영역 캡처** — BitBlt는 _스크린에 보이는 것_ 캡처. 다른 게임/앱이 그 좌표 위에 있으면 그쪽 캡처
- ⚠️ **`m_WindowPtr` reflection이 OS HWND 아님** — Unity 내부 native pointer 반환 (값 너무 큼, GetWindowRect 결과 0,0,0,0)
- 0.4.0 후속: `PrintWindow` API + Unity 메인 ContainerWindow `FindWindow` 클래스명 매칭

## [0.2.0] - 2026-05-02

### Added
- **EditMode UI 콘텐츠 캡처 작동** ⭐ — 0.1.0의 핵심 한계 해결
- `ForceRenderPanel(IPanel)` — `RuntimePanel.Update()` + `UpdateForRepaint()` + `Render()` reflection 직접 호출 패턴
- `Tools > UI Toolkit Capture > Probe > Enum Panel Methods` — Panel internal API 진단 도구
- `Tools > UI Toolkit Capture > Probe > Enum UIElementsRuntimeUtility Methods` — RuntimeUtility 진단
- `Tools > UI Toolkit Capture > Probe > Try Each Repaint Method` — 후보 메서드 자동 시도
- Probe 결과 파일 출력 (`Assets/_UIToolkitCapture/Probe/`)

### Changed
- Runtime/Editor 둘 다 `ForceUpdatePanels` (UIElementsRuntimeUtility) → `ForceRenderPanel` (RuntimePanel 직접) 교체
- Unity 6.3.12 검증 완료 — `UnityEngine.UIElements.RuntimePanel` 메서드 시그니처 의존

### Verified
- EditMode `Tools > UI Toolkit Capture > Test > EditMode UIDocumentTest` → UI 콘텐츠(한글·색상·레이아웃) 정상 캡처
- PlayMode 작동 유지 (1번 호출로 자동 렌더 사이클)

### Known Limitations (0.2.0 잔존)
- EditorWindow 캡처 — 여전히 미구현 (0.3.0 후속, EditorWindow 자체 panel은 internal IPanel 접근 별도 경로)
- 단일 Active Scene 탐색만 지원
- internal API 의존 — Unity 버전 업그레이드 시 깨질 가능성

## [0.1.0] - 2026-05-02

### Added
- 초기 릴리스 — AnimCaptureModule_MCP 패턴 정합
- `UIToolkitCapture.CaptureUIDocument(...)` Runtime API
- `UIToolkitCaptureEditor.CaptureUIDocumentEditMode(...)` Editor API
- `UIToolkitCaptureEditor.CaptureEditorWindow(...)` 실험 API (0.1.0 미작동)
- MenuItem 자동화 — `Tools > UI Toolkit Capture > ...`
- `CaptureResult` struct + Ok/Fail 패턴
- `UpdatePanels` reflection 호출로 EditMode 강제 panel update 시도
- `EditorApplication.QueuePlayerLoopUpdate` + `SceneView.RepaintAll` 보조 트리거

### Known Limitations
- EditMode 캡처 실험적 (Unity 6.3.x 작동 검증 필요)
- EditorWindow 캡처 미구현 (Panel internal API 정착 후 0.2.0)
- 단일 Active Scene 탐색만 지원
