using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Quizzer.Base
{
    public static class RichTextBoxBinder
    {
        public static readonly DependencyProperty PlainTextProperty =
            DependencyProperty.RegisterAttached(
                "PlainText",
                typeof(string),
                typeof(RichTextBoxBinder),
                new FrameworkPropertyMetadata(
                    "",
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnPlainTextChanged));

        public static string GetPlainText(DependencyObject obj) =>
            (string)obj.GetValue(PlainTextProperty);

        public static void SetPlainText(DependencyObject obj, string value) =>
            obj.SetValue(PlainTextProperty, value);

        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached(
                "IsUpdating",
                typeof(bool),
                typeof(RichTextBoxBinder),
                new PropertyMetadata(false));

        private static bool GetIsUpdating(DependencyObject obj) =>
            (bool)obj.GetValue(IsUpdatingProperty);

        private static void SetIsUpdating(DependencyObject obj, bool value) =>
            obj.SetValue(IsUpdatingProperty, value);

        private static void OnPlainTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not RichTextBox rtb) return;
            if (GetIsUpdating(rtb)) return;

            rtb.TextChanged -= Rtb_TextChanged;
            rtb.Document.Blocks.Clear();
            rtb.Document.Blocks.Add(new Paragraph(new Run(e.NewValue?.ToString() ?? "")));
            rtb.TextChanged += Rtb_TextChanged;
        }

        private static void Rtb_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not RichTextBox rtb) return;

            try
            {
                SetIsUpdating(rtb, true);

                var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                var text = range.Text;

                // RichTextBox always appends a trailing newline; trim if you don't want it
                if (text.EndsWith("\r\n")) text = text[..^2];

                SetPlainText(rtb, text);
            }
            finally
            {
                SetIsUpdating(rtb, false);
            }
        }
    }
}