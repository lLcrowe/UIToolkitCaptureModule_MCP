using System;

namespace lLCroweTool.UIToolkitCapture
{
    /// <summary>
    /// UI Toolkit 캡처 결과 구조체. AnimCaptureModule_MCP 패턴 정합.
    /// Claude MCP execute_code 호출 시 return 값으로 받음.
    /// </summary>
    [Serializable]
    public struct CaptureResult
    {
        public bool success;
        public string outputPath;
        public string error;
        public int width;
        public int height;

        public static CaptureResult Ok(string path, int w, int h)
            => new CaptureResult { success = true, outputPath = path, width = w, height = h };

        public static CaptureResult Fail(string err)
            => new CaptureResult { success = false, error = err };

        public override string ToString()
            => success ? $"OK: {outputPath} ({width}x{height})" : $"FAIL: {error}";
    }
}
