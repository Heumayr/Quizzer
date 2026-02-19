using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.Base
{
    public static class AutoFitTextBox
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(AutoFitTextBox),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

        public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);

        public static readonly DependencyProperty MinFontSizeProperty =
            DependencyProperty.RegisterAttached(
                "MinFontSize",
                typeof(double),
                typeof(AutoFitTextBox),
                new PropertyMetadata(8d));

        public static void SetMinFontSize(DependencyObject d, double value) => d.SetValue(MinFontSizeProperty, value);

        public static double GetMinFontSize(DependencyObject d) => (double)d.GetValue(MinFontSizeProperty);

        public static readonly DependencyProperty MaxFontSizeProperty =
            DependencyProperty.RegisterAttached(
                "MaxFontSize",
                typeof(double),
                typeof(AutoFitTextBox),
                new PropertyMetadata(22d));

        public static void SetMaxFontSize(DependencyObject d, double value) => d.SetValue(MaxFontSizeProperty, value);

        public static double GetMaxFontSize(DependencyObject d) => (double)d.GetValue(MaxFontSizeProperty);

        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached(
                "IsUpdating",
                typeof(bool),
                typeof(AutoFitTextBox),
                new PropertyMetadata(false));

        private static void SetIsUpdating(DependencyObject d, bool value) => d.SetValue(IsUpdatingProperty, value);

        private static bool GetIsUpdating(DependencyObject d) => (bool)d.GetValue(IsUpdatingProperty);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox tb) return;

            if ((bool)e.NewValue)
            {
                tb.Loaded += Tb_OnChanged;
                tb.SizeChanged += Tb_OnChanged;
                tb.TextChanged += Tb_OnChanged;

                // wichtig: Template laden, damit ScrollViewer existiert
                tb.Dispatcher.BeginInvoke(new Func<bool>(tb.ApplyTemplate), DispatcherPriority.Loaded);

                ScheduleUpdate(tb);
            }
            else
            {
                tb.Loaded -= Tb_OnChanged;
                tb.SizeChanged -= Tb_OnChanged;
                tb.TextChanged -= Tb_OnChanged;
            }
        }

        private static void Tb_OnChanged(object? sender, EventArgs e)
        {
            if (sender is TextBox tb) ScheduleUpdate(tb);
        }

        private static void ScheduleUpdate(TextBox tb)
        {
            if (GetIsUpdating(tb)) return;

            tb.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (GetIsUpdating(tb)) return;

                try
                {
                    SetIsUpdating(tb, true);
                    UpdateFontSize(tb);
                }
                finally
                {
                    SetIsUpdating(tb, false);
                }
            }), DispatcherPriority.Background);
        }

        private static void UpdateFontSize(TextBox tb)
        {
            if (!tb.IsLoaded) return;

            var sv = GetScrollViewer(tb);
            if (sv == null) return;

            double min = Math.Max(1, GetMinFontSize(tb));
            double max = Math.Max(min, GetMaxFontSize(tb));

            // leeren Text messbar machen
            if (string.IsNullOrEmpty(tb.Text))
            {
                tb.FontSize = max;
                return;
            }

            // Binary search
            double best = min;
            double lo = min;
            double hi = max;

            // erst mal sicherstellen, dass Layout aktuell ist
            tb.UpdateLayout();

            while (hi - lo > 0.25)
            {
                double mid = (lo + hi) / 2.0;
                if (Fits(tb, sv, mid))
                {
                    best = mid;
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            tb.FontSize = best;
        }

        private static bool Fits(TextBox tb, ScrollViewer sv, double fontSize)
        {
            tb.FontSize = fontSize;

            // UpdateLayout damit Extent/Viewport korrekt sind
            tb.UpdateLayout();
            sv.UpdateLayout();

            // kleine Toleranz gegen Rundungsfehler
            const double eps = 0.5;

            // Falls Viewport 0 ist (selten), abbrechen
            if (sv.ViewportWidth <= 0 || sv.ViewportHeight <= 0) return true;

            return sv.ExtentWidth <= sv.ViewportWidth + eps
                && sv.ExtentHeight <= sv.ViewportHeight + eps;
        }

        private static ScrollViewer? GetScrollViewer(TextBox tb)
        {
            tb.ApplyTemplate();

            // Standard-Template: PART_ContentHost ist ein ScrollViewer
            if (tb.Template?.FindName("PART_ContentHost", tb) is ScrollViewer sv)
                return sv;

            // Fallback: VisualTree durchsuchen
            return FindVisualChild<ScrollViewer>(tb);
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) return typed;

                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}