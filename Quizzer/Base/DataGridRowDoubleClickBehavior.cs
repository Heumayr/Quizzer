using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Quizzer.Base
{
    public static class DataGridRowDoubleClickBehavior
    {
        // 1) Command to run on double click
        public static readonly DependencyProperty DoubleClickCommandProperty =
            DependencyProperty.RegisterAttached(
                "DoubleClickCommand",
                typeof(ICommand),
                typeof(DataGridRowDoubleClickBehavior),
                new PropertyMetadata(null, OnDoubleClickCommandChanged));

        public static void SetDoubleClickCommand(DependencyObject element, ICommand value)
            => element.SetValue(DoubleClickCommandProperty, value);

        public static ICommand GetDoubleClickCommand(DependencyObject element)
            => (ICommand)element.GetValue(DoubleClickCommandProperty);

        // 2) Optional: write clicked item back to a bound property
        public static readonly DependencyProperty DoubleClickedItemProperty =
            DependencyProperty.RegisterAttached(
                "DoubleClickedItem",
                typeof(object),
                typeof(DataGridRowDoubleClickBehavior),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static void SetDoubleClickedItem(DependencyObject element, object value)
            => element.SetValue(DoubleClickedItemProperty, value);

        public static object GetDoubleClickedItem(DependencyObject element)
            => element.GetValue(DoubleClickedItemProperty);

        private static void OnDoubleClickCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid grid) return;

            // Avoid duplicate subscriptions
            grid.PreviewMouseDoubleClick -= Grid_PreviewMouseDoubleClick;
            if (e.NewValue != null)
                grid.PreviewMouseDoubleClick += Grid_PreviewMouseDoubleClick;
        }

        private static void Grid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid grid) return;

            // Find the DataGridRow that was double-clicked
            var row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row == null) return;

            var item = row.Item;               // <- THIS is the item from ItemsSource
            if (item == null) return;

            // Write the item to the bound property (optional)
            SetDoubleClickedItem(grid, item);

            // Execute command (optional)
            var cmd = GetDoubleClickCommand(grid);
            if (cmd != null && cmd.CanExecute(item))
            {
                cmd.Execute(item);
                e.Handled = true; // prevents default edit toggles etc.
            }
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T typed) return typed;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}