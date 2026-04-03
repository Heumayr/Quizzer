using Quizzer.Views.Base;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.Base
{
    public class WindowBase : Window
    {
        public Guid Id { get; } = Guid.NewGuid();
        public WindowTyp WindowTyp { get; set; } = WindowTyp.Menu;

        private static readonly WindowPlacementStore PlacementStore = new("Quizzer");

        public virtual bool UsePlacementPersistence => true;

        // default: normal maximize behavior (taskbar stays visible)
        public virtual bool FullscreenOverTaskbar => false;

        public string? PlacementKey { get; set; }

        private bool _restored;
        private DateTime _lastSave = DateTime.MinValue;
        private bool _sourceInitialized;
        private bool _chromeApplyQueued;

        public WindowBase()
        {
            Loaded += WindowBase_Loaded;
            DataContextChanged += WindowBase_DataContextChanged;

            Closing += (_, __) => SavePlacement();
            LocationChanged += (_, __) => SavePlacementThrottled();
            SizeChanged += (_, __) => SavePlacementThrottled();
            StateChanged += (_, __) => SavePlacementThrottled();

            KeyDown += DefaultKeyDown;

            ChromeBackground = Colors.Black;
            ChromeForeground = Colors.WhiteSmoke;
            ChromeBorderColor = Colors.Black;
            UseDarkChrome = true;
        }

        #region Chrome Properties

        public Color ChromeBackground
        {
            get => (Color)GetValue(ChromeBackgroundProperty);
            set => SetValue(ChromeBackgroundProperty, value);
        }

        public static readonly DependencyProperty ChromeBackgroundProperty =
            DependencyProperty.Register(
                nameof(ChromeBackground),
                typeof(Color),
                typeof(WindowBase),
                new PropertyMetadata(Colors.DarkBlue, OnChromePropertyChanged));

        public Color ChromeForeground
        {
            get => (Color)GetValue(ChromeForegroundProperty);
            set => SetValue(ChromeForegroundProperty, value);
        }

        public static readonly DependencyProperty ChromeForegroundProperty =
            DependencyProperty.Register(
                nameof(ChromeForeground),
                typeof(Color),
                typeof(WindowBase),
                new PropertyMetadata(Colors.WhiteSmoke, OnChromePropertyChanged));

        public Color ChromeBorderColor
        {
            get => (Color)GetValue(ChromeBorderColorProperty);
            set => SetValue(ChromeBorderColorProperty, value);
        }

        public static readonly DependencyProperty ChromeBorderColorProperty =
            DependencyProperty.Register(
                nameof(ChromeBorderColor),
                typeof(Color),
                typeof(WindowBase),
                new PropertyMetadata(Colors.DarkBlue, OnChromePropertyChanged));

        public bool UseDarkChrome
        {
            get => (bool)GetValue(UseDarkChromeProperty);
            set => SetValue(UseDarkChromeProperty, value);
        }

        public static readonly DependencyProperty UseDarkChromeProperty =
            DependencyProperty.Register(
                nameof(UseDarkChrome),
                typeof(bool),
                typeof(WindowBase),
                new PropertyMetadata(true, OnChromePropertyChanged));

        private static void OnChromePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WindowBase window)
                window.QueueApplyChrome();
        }

        #endregion Chrome Properties

        private void DefaultKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
                hwndSource.AddHook(WindowProc);

            _sourceInitialized = true;
            QueueApplyChrome();
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO && FullscreenOverTaskbar)
            {
                ApplyFullscreenMaxBounds(hwnd, lParam);
                handled = true;
            }

            return IntPtr.Zero;
        }

        private static void ApplyFullscreenMaxBounds(IntPtr hwnd, IntPtr lParam)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf<MONITORINFO>();

                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    RECT rcMonitor = monitorInfo.rcMonitor;

                    // use full monitor area, not work area -> covers taskbar
                    mmi.ptMaxPosition.x = 0;
                    mmi.ptMaxPosition.y = 0;
                    mmi.ptMaxSize.x = rcMonitor.right - rcMonitor.left;
                    mmi.ptMaxSize.y = rcMonitor.bottom - rcMonitor.top;
                    mmi.ptMaxTrackSize.x = mmi.ptMaxSize.x;
                    mmi.ptMaxTrackSize.y = mmi.ptMaxSize.y;
                }
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        private void WindowBase_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ViewModelBase newVm)
                newVm.Window = this;
        }

        private async void WindowBase_Loaded(object sender, RoutedEventArgs e)
        {
            if (UsePlacementPersistence)
                RestorePlacement();

            QueueApplyChrome();

            if (DataContext is ViewModelBase vm)
                await vm.InitializeAsync();
        }

        private void QueueApplyChrome()
        {
            if (_chromeApplyQueued)
                return;

            _chromeApplyQueued = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _chromeApplyQueued = false;

                if (!_sourceInitialized)
                    return;

                ApplyChrome();
            }), DispatcherPriority.Loaded);
        }

        public virtual void ApplyChrome()
        {
            ApplyNativeTitleBarColors();
        }

        private void ApplyNativeTitleBarColors()
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
                return;

            try
            {
                int darkMode = UseDarkChrome ? 1 : 0;
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, Marshal.SizeOf<int>());

                uint captionColor = ToDwmColor(ChromeBackground);
                DwmSetWindowAttribute(handle, DWMWA_CAPTION_COLOR, ref captionColor, Marshal.SizeOf<uint>());

                uint textColor = ToDwmColor(ChromeForeground);
                DwmSetWindowAttribute(handle, DWMWA_TEXT_COLOR, ref textColor, Marshal.SizeOf<uint>());

                uint borderColor = ToDwmColor(ChromeBorderColor);
                DwmSetWindowAttribute(handle, DWMWA_BORDER_COLOR, ref borderColor, Marshal.SizeOf<uint>());
            }
            catch
            {
            }
        }

        private static uint ToDwmColor(Color color)
        {
            return (uint)(color.R | (color.G << 8) | (color.B << 16));
        }

        private string GetKey() => PlacementKey ?? GetType().FullName ?? GetType().Name;

        private void RestorePlacement()
        {
            if (_restored) return;
            _restored = true;

            if (!PlacementStore.TryGet(GetKey(), out var p) || p == null)
                return;

            if (p.Width > 100) Width = p.Width;
            if (p.Height > 100) Height = p.Height;

            Left = p.Left;
            Top = p.Top;

            EnsureOnScreen();

            var state = (WindowState)p.WindowState;
            if (state == WindowState.Maximized || state == WindowState.Normal)
                WindowState = state;
        }

        private void SavePlacement()
        {
            if (!UsePlacementPersistence)
                return;

            Rect bounds = WindowState == WindowState.Normal
                ? new Rect(Left, Top, Width, Height)
                : RestoreBounds;

            if (bounds.Width < 100 || bounds.Height < 100)
                return;

            PlacementStore.Set(GetKey(), new WindowPlacement
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                WindowState = (int)(WindowState == WindowState.Minimized ? WindowState.Normal : WindowState)
            });
        }

        private void SavePlacementThrottled()
        {
            if (!UsePlacementPersistence)
                return;

            if (!_restored) return;

            var now = DateTime.UtcNow;
            if ((now - _lastSave).TotalMilliseconds < 300) return;

            _lastSave = now;
            SavePlacement();
        }

        private bool _isFullscreen;
        private WindowStyle _restoreWindowStyle;
        private ResizeMode _restoreResizeMode;
        private WindowState _restoreWindowState;
        private bool _restoreTopmost;
        private Rect _restoreBounds;

        public void SetFullscreen(bool fullscreen)
        {
            if (fullscreen)
            {
                if (_isFullscreen)
                    return;

                _isFullscreen = true;

                _restoreWindowStyle = WindowStyle;
                _restoreResizeMode = ResizeMode;
                _restoreWindowState = WindowState;
                _restoreTopmost = Topmost;
                _restoreBounds = WindowState == WindowState.Normal
                    ? new Rect(Left, Top, Width, Height)
                    : RestoreBounds;

                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                Topmost = FullscreenOverTaskbar;
                WindowState = WindowState.Maximized;

                return;
            }

            if (!_isFullscreen)
                return;

            _isFullscreen = false;

            WindowState = WindowState.Normal;
            WindowStyle = _restoreWindowStyle;
            ResizeMode = _restoreResizeMode;
            Topmost = _restoreTopmost;

            Left = _restoreBounds.Left;
            Top = _restoreBounds.Top;
            Width = _restoreBounds.Width;
            Height = _restoreBounds.Height;

            WindowState = _restoreWindowState;
        }

        public void ToggleFullscreen() => SetFullscreen(!_isFullscreen);

        private void EnsureOnScreen()
        {
            var left = SystemParameters.VirtualScreenLeft;
            var top = SystemParameters.VirtualScreenTop;
            var width = SystemParameters.VirtualScreenWidth;
            var height = SystemParameters.VirtualScreenHeight;

            var minX = left;
            var minY = top;
            var maxX = left + width - 50;
            var maxY = top + height - 50;

            if (Left < minX) Left = minX;
            if (Top < minY) Top = minY;
            if (Left > maxX) Left = maxX;
            if (Top > maxY) Top = maxY;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }

        #region DWM Interop

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            ref int pvAttribute,
            int cbAttribute);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            ref uint pvAttribute,
            int cbAttribute);

        #endregion DWM Interop

        #region Fullscreen / Monitor Interop

        private const int WM_GETMINMAXINFO = 0x0024;
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        #endregion Fullscreen / Monitor Interop
    }
}