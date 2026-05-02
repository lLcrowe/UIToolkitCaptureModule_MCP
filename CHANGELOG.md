# Changelog

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
