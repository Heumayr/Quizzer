using System.Windows;
using System.Windows.Controls;

namespace Quizzer.Base
{
    public static class MatrixGridBehavior
    {
        public static readonly DependencyProperty RowsProperty =
            DependencyProperty.RegisterAttached(
                "Rows", typeof(int), typeof(MatrixGridBehavior),
                new PropertyMetadata(0, OnChanged));

        public static void SetRows(DependencyObject d, int value) => d.SetValue(RowsProperty, value);

        public static int GetRows(DependencyObject d) => (int)d.GetValue(RowsProperty);

        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.RegisterAttached(
                "Columns", typeof(int), typeof(MatrixGridBehavior),
                new PropertyMetadata(0, OnChanged));

        public static void SetColumns(DependencyObject d, int value) => d.SetValue(ColumnsProperty, value);

        public static int GetColumns(DependencyObject d) => (int)d.GetValue(ColumnsProperty);

        public static readonly DependencyProperty CellWidthProperty =
            DependencyProperty.RegisterAttached(
                "CellWidth", typeof(double), typeof(MatrixGridBehavior),
                new PropertyMetadata(80d, OnChanged));

        public static void SetCellWidth(DependencyObject d, double value) => d.SetValue(CellWidthProperty, value);

        public static double GetCellWidth(DependencyObject d) => (double)d.GetValue(CellWidthProperty);

        public static readonly DependencyProperty CellHeightProperty =
            DependencyProperty.RegisterAttached(
                "CellHeight", typeof(double), typeof(MatrixGridBehavior),
                new PropertyMetadata(50d, OnChanged));

        public static void SetCellHeight(DependencyObject d, double value) => d.SetValue(CellHeightProperty, value);

        public static double GetCellHeight(DependencyObject d) => (double)d.GetValue(CellHeightProperty);

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Grid g) return;
            Rebuild(g);
        }

        private static void Rebuild(Grid g)
        {
            int rows = GetRows(g);
            int cols = GetColumns(g);
            double cellW = GetCellWidth(g);
            double cellH = GetCellHeight(g);

            g.RowDefinitions.Clear();
            g.ColumnDefinitions.Clear();

            if (rows <= 0 || cols <= 0) return;

            // +1 for headers
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cellH) }); // header row
            for (int r = 0; r < rows; r++)
                g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cellH) });

            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cellW) }); // header col
            for (int c = 0; c < cols; c++)
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cellW) });
        }
    }
}