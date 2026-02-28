using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.Base
{
    public static class AutoFitLabel
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(AutoFitLabel),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

        public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);

        public static readonly DependencyProperty MinFontSizeProperty =
            DependencyProperty.RegisterAttached(
                "MinFontSize",
                typeof(double),
                typeof(AutoFitLabel),
                new PropertyMetadata(8d));

        public static void SetMinFontSize(DependencyObject d, double value) => d.SetValue(MinFontSizeProperty, value);

        public static double GetMinFontSize(DependencyObject d) => (double)d.GetValue(MinFontSizeProperty);

        public static readonly DependencyProperty MaxFontSizeProperty =
            DependencyProperty.RegisterAttached(
                "MaxFontSize",
                typeof(double),
                typeof(AutoFitLabel),
                new PropertyMetadata(22d));

        public static void SetMaxFontSize(DependencyObject d, double value) => d.SetValue(MaxFontSizeProperty, value);

        public static double GetMaxFontSize(DependencyObject d) => (double)d.GetValue(MaxFontSizeProperty);

        /// <summary>Optional: enable wrapping for the label text (helps when height is limited).</summary>
        public static readonly DependencyProperty WrapTextProperty =
            DependencyProperty.RegisterAttached(
                "WrapText",
                typeof(bool),
                typeof(AutoFitLabel),
                new PropertyMetadata(false, OnWrapChanged));

        public static void SetWrapText(DependencyObject d, bool value) => d.SetValue(WrapTextProperty, value);

        public static bool GetWrapText(DependencyObject d) => (bool)d.GetValue(WrapTextProperty);

        private static void OnWrapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Label lb && GetIsEnabled(lb))
                ScheduleUpdate(lb);
        }

        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached(
                "IsUpdating",
                typeof(bool),
                typeof(AutoFitLabel),
                new PropertyMetadata(false));

        private static void SetIsUpdating(DependencyObject d, bool value) => d.SetValue(IsUpdatingProperty, value);

        private static bool GetIsUpdating(DependencyObject d) => (bool)d.GetValue(IsUpdatingProperty);

        private static readonly DependencyProperty ContentHandlerProperty =
            DependencyProperty.RegisterAttached(
                "ContentHandler",
                typeof(EventHandler),
                typeof(AutoFitLabel),
                new PropertyMetadata(null));

        private static void SetContentHandler(DependencyObject d, EventHandler? value) => d.SetValue(ContentHandlerProperty, value);

        private static EventHandler? GetContentHandler(DependencyObject d) => (EventHandler?)d.GetValue(ContentHandlerProperty);

        private static readonly DependencyPropertyDescriptor ContentDescriptor =
            DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(Label));

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Label lb) return;

            if ((bool)e.NewValue)
            {
                lb.Loaded += Lb_OnChanged;
                lb.SizeChanged += Lb_OnChanged;

                // watch Content changes
                EventHandler handler = (_, __) => ScheduleUpdate(lb);
                ContentDescriptor.AddValueChanged(lb, handler);
                SetContentHandler(lb, handler);

                ScheduleUpdate(lb);
            }
            else
            {
                lb.Loaded -= Lb_OnChanged;
                lb.SizeChanged -= Lb_OnChanged;

                var handler = GetContentHandler(lb);
                if (handler != null)
                {
                    ContentDescriptor.RemoveValueChanged(lb, handler);
                    SetContentHandler(lb, null);
                }
            }
        }

        private static void Lb_OnChanged(object? sender, EventArgs e)
        {
            if (sender is Label lb) ScheduleUpdate(lb);
        }

        private static void ScheduleUpdate(Label lb)
        {
            if (GetIsUpdating(lb)) return;

            lb.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (GetIsUpdating(lb)) return;

                try
                {
                    SetIsUpdating(lb, true);
                    UpdateFontSize(lb);
                }
                finally
                {
                    SetIsUpdating(lb, false);
                }
            }), DispatcherPriority.Background);
        }

        private static void UpdateFontSize(Label lb)
        {
            if (!lb.IsLoaded) return;

            // Only handle textual content (string / numbers / enums). Skip UIElement content.
            if (lb.Content is UIElement) return;

            var text = lb.Content?.ToString() ?? string.Empty;

            double min = Math.Max(1, GetMinFontSize(lb));
            double max = Math.Max(min, GetMaxFontSize(lb));

            // available space (subtract padding)
            var pad = lb.Padding;
            double availW = Math.Max(0, lb.ActualWidth - pad.Left - pad.Right);
            double availH = Math.Max(0, lb.ActualHeight - pad.Top - pad.Bottom);

            if (availW <= 0 || availH <= 0) return;

            // empty text -> just use max
            if (string.IsNullOrEmpty(text))
            {
                lb.FontSize = max;
                return;
            }

            bool wrap = GetWrapText(lb);

            double best = min;
            double lo = min;
            double hi = max;

            while (hi - lo > 0.25)
            {
                double mid = (lo + hi) / 2.0;

                if (Fits(lb, text, mid, availW, availH, wrap))
                {
                    best = mid;
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            lb.FontSize = best;
        }

        private static bool Fits(Label lb, string text, double fontSize, double availW, double availH, bool wrap)
        {
            const double eps = 0.5;

            var dpi = VisualTreeHelper.GetDpi(lb).PixelsPerDip;

            var ft = new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                lb.FlowDirection,
                new Typeface(lb.FontFamily, lb.FontStyle, lb.FontWeight, lb.FontStretch),
                fontSize,
                Brushes.Black,
                dpi);

            if (wrap)
                ft.MaxTextWidth = availW; // enables wrapping for measurement

            // WidthIncludingTrailingWhitespace gives more stable results for centering layouts
            var w = ft.WidthIncludingTrailingWhitespace;
            var h = ft.Height;

            return w <= availW + eps && h <= availH + eps;
        }
    }
}