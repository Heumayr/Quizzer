using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace Quizzer.Views.GameViews.QuestionViews.Typed.Media
{
    public static class ScreenHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

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

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        public static Rect GetMonitorBoundsDip(Window referenceWindow, bool useWorkArea = false)
        {
            if (referenceWindow == null)
            {
                return new Rect(
                    0,
                    0,
                    SystemParameters.PrimaryScreenWidth,
                    SystemParameters.PrimaryScreenHeight);
            }

            Point centerDip = new(
                referenceWindow.Left + (referenceWindow.Width / 2.0),
                referenceWindow.Top + (referenceWindow.Height / 2.0));

            POINT centerPx = ToDevicePixels(referenceWindow, centerDip);

            IntPtr monitor = MonitorFromPoint(centerPx, MONITOR_DEFAULTTONEAREST);

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

            RECT raw = useWorkArea ? info.rcWork : info.rcMonitor;

            return ToDipRect(referenceWindow, raw);
        }

        private static POINT ToDevicePixels(Window window, Point pointDip)
        {
            PresentationSource? source = PresentationSource.FromVisual(window);

            if (source?.CompositionTarget != null)
            {
                Matrix toDevice = source.CompositionTarget.TransformToDevice;
                Point pointPx = toDevice.Transform(pointDip);

                return new POINT
                {
                    X = (int)Math.Round(pointPx.X),
                    Y = (int)Math.Round(pointPx.Y)
                };
            }

            return new POINT
            {
                X = (int)Math.Round(pointDip.X),
                Y = (int)Math.Round(pointDip.Y)
            };
        }

        private static Rect ToDipRect(Window window, RECT rectPx)
        {
            PresentationSource? source = PresentationSource.FromVisual(window);

            if (source?.CompositionTarget != null)
            {
                Matrix fromDevice = source.CompositionTarget.TransformFromDevice;

                Point topLeft = fromDevice.Transform(new Point(rectPx.Left, rectPx.Top));
                Point bottomRight = fromDevice.Transform(new Point(rectPx.Right, rectPx.Bottom));

                return new Rect(topLeft, bottomRight);
            }

            return new Rect(
                rectPx.Left,
                rectPx.Top,
                rectPx.Right - rectPx.Left,
                rectPx.Bottom - rectPx.Top);
        }
    }
}