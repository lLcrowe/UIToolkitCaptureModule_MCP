# Changelog

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
