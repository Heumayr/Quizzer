using Quizzer.Views.Base;
using System;
using System.Security.Principal;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Quizzer.Base
{
    public class WindowBase : Window
    {
        public Guid Id { get; } = Guid.NewGuid();
        public WindowTyp WindowTyp { get; set; } = WindowTyp.Menu;

        private static readonly WindowPlacementStore PlacementStore = new("Quizzer");

        /// <summary>
        /// Optional: override per window instance.
        /// By default we use the window type full name.
        /// </summary>
        public string? PlacementKey { get; set; }

        private bool _restored;

        public WindowBase()
        {
            Loaded += WindowBase_Loaded;
            DataContextChanged += WindowBase_DataContextChanged;

            // Save when closing
            Closing += (_, __) => SavePlacement();

            // Save when user drags/resizes
            LocationChanged += (_, __) => SavePlacementThrottled();
            SizeChanged += (_, __) => SavePlacementThrottled();
            StateChanged += (_, __) => SavePlacementThrottled();

            KeyDown += (s, e) => DefaultKeyDown(s, e);
        }

        private void DefaultKeyDown(object s, KeyEventArgs e)
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
        }

        private void WindowBase_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ViewModelBase newVm)
                newVm.Window = this;
        }

        private async void WindowBase_Loaded(object sender, RoutedEventArgs e)
        {
            RestorePlacement();

            if (DataContext is ViewModelBase vm)
                await vm.InitializeAsync();
        }

        private string GetKey() => PlacementKey ?? GetType().FullName ?? GetType().Name;

        private void RestorePlacement()
        {
            if (_restored) return;
            _restored = true;

            if (!PlacementStore.TryGet(GetKey(), out var p))
                return;

            if (p == null)
                return;

            // restore normal size/pos only if valid
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
            // If minimized, save RestoreBounds instead of tiny minimized bounds
            Rect bounds = WindowState == WindowState.Normal
                ? new Rect(Left, Top, Width, Height)
                : RestoreBounds;

            // If not yet shown / not measured
            if (bounds.Width < 100 || bounds.Height < 100) return;

            PlacementStore.Set(GetKey(), new WindowPlacement
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                WindowState = (int)(WindowState == WindowState.Minimized ? WindowState.Normal : WindowState)
            });
        }

        private DateTime _lastSave = DateTime.MinValue;

        private void SavePlacementThrottled()
        {
            if (!_restored) return;

            var now = DateTime.UtcNow;
            if ((now - _lastSave).TotalMilliseconds < 300) return;
            _lastSave = now;

            SavePlacement();
        }

        private void EnsureOnScreen()
        {
            // If user changed monitor setup, window might restore off-screen.
            // We just clamp it into the current virtual screen bounds.
            var vs = SystemParameters.WorkArea; // primary work area
            // Better multi-monitor clamp:
            // Use SystemParameters.VirtualScreen* to include all monitors.
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
    }
}