using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Quizzer.Views.GameViews.QuestionViews.Typed.Media
{
    public static class ScreenHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        public static Rect GetMonitorBoundsPx(Window referenceWindow, bool useWorkArea = false)
        {
            if (referenceWindow == null)
            {
                return new Rect(
                    0,
                    0,
                    SystemParameters.PrimaryScreenWidth,
                    SystemParameters.PrimaryScreenHeight);
            }

            IntPtr hwnd = new WindowInteropHelper(referenceWindow).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return new Rect(
                    0,
                    0,
                    SystemParameters.PrimaryScreenWidth,
                    SystemParameters.PrimaryScreenHeight);
            }

            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            MONITORINFO info = new();
            info.cbSize = Marshal.SizeOf<MONITORINFO>();

            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            {
                return new Rect(
                    0,
                    0,
                    SystemParameters.PrimaryScreenWidth,
                    SystemParameters.PrimaryScreenHeight);
            }

            var raw = useWorkArea ? info.rcWork : info.rcMonitor;

            return new Rect(
                raw.Left,
                raw.Top,
                raw.Right - raw.Left,
                raw.Bottom - raw.Top);
        }

        public static void MoveToMonitor(Window targetWindow, Window referenceWindow, bool useWorkArea = false)
        {
            if (targetWindow == null || referenceWindow == null)
                return;

            IntPtr targetHwnd = new WindowInteropHelper(targetWindow).Handle;
            if (targetHwnd == IntPtr.Zero)
                return;

            Rect bounds = GetMonitorBoundsPx(referenceWindow, useWorkArea);

            SetWindowPos(
                targetHwnd,
                IntPtr.Zero,
                (int)bounds.Left,
                (int)bounds.Top,
                (int)bounds.Width,
                (int)bounds.Height,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
    }
}