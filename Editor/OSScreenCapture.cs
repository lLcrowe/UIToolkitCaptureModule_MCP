using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace lLCroweTool.UIToolkitCapture.Editor
{
    /// <summary>
    /// Windows OS-level 스크린 캡처 (P/Invoke BitBlt).
    /// EditorWindow는 RT에 그릴 경로 없어서 OS 캡처 + crop 패턴.
    /// 0.3.0 신설 — Windows 전용 (다른 플랫폼은 추후 분기).
    /// </summary>
    public static class OSScreenCapture
    {
        [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hDC, int x, int y, int w, int h, IntPtr hSrcDC, int sx, int sy, int rop);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int w, int h);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr h);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, int start, int cLines, [Out] byte[] lpvBits, ref BITMAPINFO lpbi, int usage);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const uint PW_RENDERFULLCONTENT = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public int Width => right - left;
            public int Height => bottom - top;
        }

        /// <summary>
        /// EditorWindow의 ContainerWindow OS HWND를 reflection chain으로 추출.
        /// chain: EditorWindow.m_Parent (HostView) → window (ContainerWindow) → m_WindowPtr (MonoReloadableIntPtr) → IntPtr 필드.
        /// Unity 6 internal API 의존 — 버전 변경 시 깨질 수 있음 (필드명 자동 탐색으로 일부 보강).
        /// </summary>
        public static IntPtr GetEditorWindowHWND(EditorWindow window)
        {
            if (window == null) return IntPtr.Zero;

            try
            {
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // EditorWindow → m_Parent (HostView)
                var parentField = typeof(EditorWindow).GetField("m_Parent", flags);
                var parent = parentField?.GetValue(window);
                if (parent == null) return IntPtr.Zero;

                // HostView → window (ContainerWindow)
                var windowField = parent.GetType().GetField("window", flags)
                                  ?? parent.GetType().GetField("m_Window", flags);
                var container = windowField?.GetValue(parent);
                if (container == null)
                {
                    var prop = parent.GetType().GetProperty("window", flags);
                    container = prop?.GetValue(parent);
                }
                if (container == null) return IntPtr.Zero;

                // ContainerWindow → m_WindowPtr (MonoReloadableIntPtr)
                var winPtrField = container.GetType().GetField("m_WindowPtr", flags);
                var monoPtr = winPtrField?.GetValue(container);
                if (monoPtr == null) return IntPtr.Zero;

                // MonoReloadableIntPtr 안의 IntPtr 필드 탐색 (필드명 m_IntPtr 또는 비슷)
                var monoFields = monoPtr.GetType().GetFields(flags);
                foreach (var f in monoFields)
                {
                    if (f.FieldType == typeof(IntPtr))
                    {
                        return (IntPtr)f.GetValue(monoPtr);
                    }
                }

                return IntPtr.Zero;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>EditorWindow의 ContainerWindow OS 영역 (절대 데스크톱 좌표).</summary>
        public static RECT GetEditorWindowRect(EditorWindow window)
        {
            var hwnd = GetEditorWindowHWND(window);
            if (hwnd == IntPtr.Zero) return default;
            GetWindowRect(hwnd, out var rect);
            return rect;
        }

        /// <summary>
        /// 0.4.0 — 현재 프로세스의 가시 윈도우 중 클래스명이 _Unity_로 시작하는 첫 윈도우 HWND.
        /// EnumWindows + GetWindowThreadProcessId 매칭 + GetClassName 패턴.
        /// 다중 ContainerWindow 환경에서는 _가장 큰 영역_ 또는 _첫 매칭_ 반환.
        /// </summary>
        public static IntPtr FindUnityEditorWindow()
        {
            int currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            IntPtr foundHwnd = IntPtr.Zero;
            int foundArea = 0;

            EnumWindows((hwnd, lParam) =>
            {
                if (!IsWindowVisible(hwnd)) return true;

                GetWindowThreadProcessId(hwnd, out int pid);
                if (pid != currentPid) return true;

                var sb = new StringBuilder(256);
                GetClassName(hwnd, sb, sb.Capacity);
                var className = sb.ToString();

                // Unity Editor ContainerWindow는 _Unity_ 또는 _UnityWndClass_ 같은 클래스명
                if (!className.StartsWith("Unity", StringComparison.OrdinalIgnoreCase)) return true;

                GetWindowRect(hwnd, out var rect);
                int area = rect.Width * rect.Height;
                if (area > foundArea)
                {
                    foundArea = area;
                    foundHwnd = hwnd;
                }
                return true;
            }, IntPtr.Zero);

            return foundHwnd;
        }

        /// <summary>
        /// 0.5.0 — Unity Editor 메인 ContainerWindow 절대 데스크톱 좌표 영역.
        /// FindUnityEditorWindow + GetWindowRect 합산.
        /// </summary>
        public static RECT FindAndGetUnityEditorRect()
        {
            var hwnd = FindUnityEditorWindow();
            if (hwnd == IntPtr.Zero) return default;
            GetWindowRect(hwnd, out var rect);
            return rect;
        }

        /// <summary>
        /// 0.5.0 — Texture2D crop. EditorWindow 영역만 추출용.
        /// 입력 좌표는 _top-down_ (좌상단 0,0). Unity Texture2D는 bottom-up이라 내부 변환.
        /// 호출자가 DestroyImmediate 책임.
        /// </summary>
        public static Texture2D CropTexture(Texture2D source, int xTopDown, int yTopDown, int w, int h)
        {
            if (source == null || w <= 0 || h <= 0) return null;

            // 경계 clamp — 음수 좌표 또는 영역 초과 보정
            xTopDown = Mathf.Clamp(xTopDown, 0, source.width);
            yTopDown = Mathf.Clamp(yTopDown, 0, source.height);
            w = Mathf.Min(w, source.width - xTopDown);
            h = Mathf.Min(h, source.height - yTopDown);
            if (w <= 0 || h <= 0) return null;

            // top-down → Unity bottom-up 변환
            int yUnity = source.height - yTopDown - h;

            var pixels = source.GetPixels(xTopDown, yUnity, w, h);
            var cropped = new Texture2D(w, h, TextureFormat.RGBA32, false);
            cropped.SetPixels(pixels);
            cropped.Apply();
            return cropped;
        }

        /// <summary>
        /// 0.4.0 — PrintWindow API로 윈도우 자체 내용 캡처. BitBlt와 달리 _가려진 윈도우_도 캡처 가능.
        /// hwnd가 가리키는 윈도우 영역을 PNG로 저장.
        /// 단 Unity 6.3 EditorWindow는 child window라 자기 HWND 없을 수 있음 — 메인 ContainerWindow 캡처 후 crop 권장.
        /// </summary>
        public static Texture2D CaptureWindowPrint(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return null;

            GetWindowRect(hwnd, out var rect);
            int w = rect.Width;
            int h = rect.Height;
            if (w <= 0 || h <= 0) return null;

            IntPtr screenDC = IntPtr.Zero;
            IntPtr memDC = IntPtr.Zero;
            IntPtr bmp = IntPtr.Zero;
            IntPtr oldBmp = IntPtr.Zero;

            try
            {
                screenDC = GetDC(IntPtr.Zero);
                memDC = CreateCompatibleDC(screenDC);
                bmp = CreateCompatibleBitmap(screenDC, w, h);
                oldBmp = SelectObject(memDC, bmp);

                // PrintWindow — 윈도우 자체에 그려진 내용 캡처 (가려져도 OK)
                if (!PrintWindow(hwnd, memDC, PW_RENDERFULLCONTENT))
                {
                    Debug.LogWarning("[OSScreenCapture] PrintWindow 실패 — BitBlt fallback");
                    BitBlt(memDC, 0, 0, w, h, screenDC, rect.left, rect.top, SRCCOPY);
                }

                // 픽셀 추출
                var bi = new BITMAPINFO();
                bi.bmiHeader.biSize = Marshal.SizeOf<BITMAPINFOHEADER>();
                bi.bmiHeader.biWidth = w;
                bi.bmiHeader.biHeight = -h;
                bi.bmiHeader.biPlanes = 1;
                bi.bmiHeader.biBitCount = 32;
                bi.bmiHeader.biCompression = BI_RGB;

                var buf = new byte[w * h * 4];
                GetDIBits(memDC, bmp, 0, h, buf, ref bi, DIB_RGB_COLORS);

                // BGRA → RGBA + 상하 반전
                var rgba = new byte[buf.Length];
                for (int row = 0; row < h; row++)
                {
                    int srcRow = row * w * 4;
                    int dstRow = (h - 1 - row) * w * 4;
                    for (int col = 0; col < w; col++)
                    {
                        int s = srcRow + col * 4;
                        int d = dstRow + col * 4;
                        rgba[d + 0] = buf[s + 2];
                        rgba[d + 1] = buf[s + 1];
                        rgba[d + 2] = buf[s + 0];
                        rgba[d + 3] = 255;
                    }
                }

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.LoadRawTextureData(rgba);
                tex.Apply();
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogError($"[OSScreenCapture] CaptureWindowPrint 예외: {e.Message}");
                return null;
            }
            finally
            {
                if (oldBmp != IntPtr.Zero) SelectObject(memDC, oldBmp);
                if (bmp != IntPtr.Zero) DeleteObject(bmp);
                if (memDC != IntPtr.Zero) DeleteDC(memDC);
                if (screenDC != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDC);
            }
        }

        /// <summary>
        /// 현재 Unity Editor 메인 윈도우의 client area 좌상단 절대 데스크톱 좌표.
        /// EditorWindow.position이 client area 기준이므로 본 오프셋을 더해야 절대 좌표.
        /// </summary>
        public static (int x, int y) GetEditorClientOrigin()
        {
            try
            {
                var hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                if (hwnd == IntPtr.Zero) return (0, 0);

                var pt = new POINT { x = 0, y = 0 };
                ClientToScreen(hwnd, ref pt);
                return (pt.x, pt.y);
            }
            catch
            {
                return (0, 0);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public int bmiColors;
        }

        private const int SRCCOPY = 0x00CC0020;
        private const int DIB_RGB_COLORS = 0;
        private const int BI_RGB = 0;

        /// <summary>
        /// 스크린 영역(x, y, w, h)을 절대 좌표 기반으로 캡처해 Texture2D 반환.
        /// 호출자가 DestroyImmediate 책임.
        /// </summary>
        public static Texture2D CaptureScreenRegion(int x, int y, int w, int h)
        {
            if (w <= 0 || h <= 0) return null;

            IntPtr desktop = IntPtr.Zero;
            IntPtr src = IntPtr.Zero;
            IntPtr dest = IntPtr.Zero;
            IntPtr bmp = IntPtr.Zero;
            IntPtr oldBmp = IntPtr.Zero;

            try
            {
                desktop = GetDesktopWindow();
                src = GetDC(desktop);
                if (src == IntPtr.Zero) return null;

                dest = CreateCompatibleDC(src);
                bmp = CreateCompatibleBitmap(src, w, h);
                oldBmp = SelectObject(dest, bmp);

                BitBlt(dest, 0, 0, w, h, src, x, y, SRCCOPY);

                // GetDIBits로 픽셀 추출 (top-down: biHeight 음수)
                var bi = new BITMAPINFO();
                bi.bmiHeader.biSize = Marshal.SizeOf<BITMAPINFOHEADER>();
                bi.bmiHeader.biWidth = w;
                bi.bmiHeader.biHeight = -h;
                bi.bmiHeader.biPlanes = 1;
                bi.bmiHeader.biBitCount = 32;
                bi.bmiHeader.biCompression = BI_RGB;

                var buf = new byte[w * h * 4];
                GetDIBits(dest, bmp, 0, h, buf, ref bi, DIB_RGB_COLORS);

                // BGRA → RGBA + 상하 반전 (Unity Texture2D는 bottom-up)
                var rgba = new byte[buf.Length];
                for (int row = 0; row < h; row++)
                {
                    int srcRow = row * w * 4;
                    int dstRow = (h - 1 - row) * w * 4;
                    for (int col = 0; col < w; col++)
                    {
                        int s = srcRow + col * 4;
                        int d = dstRow + col * 4;
                        rgba[d + 0] = buf[s + 2]; // R ← B
                        rgba[d + 1] = buf[s + 1]; // G
                        rgba[d + 2] = buf[s + 0]; // B ← R
                        rgba[d + 3] = 255;        // alpha 강제 불투명
                    }
                }

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.LoadRawTextureData(rgba);
                tex.Apply();
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogError($"[OSScreenCapture] 예외: {e.Message}");
                return null;
            }
            finally
            {
                if (oldBmp != IntPtr.Zero) SelectObject(dest, oldBmp);
                if (bmp != IntPtr.Zero) DeleteObject(bmp);
                if (dest != IntPtr.Zero) DeleteDC(dest);
                if (src != IntPtr.Zero) ReleaseDC(desktop, src);
            }
        }
    }
}
