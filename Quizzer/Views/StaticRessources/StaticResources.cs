using Quizzer.DataModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Quizzer.Views.StaticRessources
{
    public static class StaticResources
    {
        private static Brush CreateImageBrushOrBlack(string? path, Stretch stretch = Stretch.Fill)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Brushes.Black;

            try
            {
                if (!Uri.TryCreate(path, UriKind.Absolute, out var uri))
                    return Brushes.Black;

                if (uri.IsFile && !File.Exists(uri.LocalPath))
                    return Brushes.Black;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                var brush = new ImageBrush(bitmap)
                {
                    Stretch = stretch
                };
                brush.Freeze();

                return brush;
            }
            catch
            {
                return Brushes.Black;
            }
        }

        public static readonly Brush CellImageBrush =
            CreateImageBrushOrBlack(Settings.CellBackgroundImagePath);

        public static readonly Brush CellHoverImageBrush =
            CreateImageBrushOrBlack(Settings.CellBackgroundHoverImagePath);

        public static readonly Brush CellImageBrushIsDone =
            CreateImageBrushOrBlack(Settings.CellBackgroundIsDoneImagePath);

        public static readonly Brush HeaderColumnImageBrush =
            CreateImageBrushOrBlack(Settings.HeaderColumnBackgroundImagePath);

        public static readonly Brush HeaderRowImageBrush =
            CreateImageBrushOrBlack(Settings.HeaderRowBackgroundImagePath);

        public static readonly Brush ChoiceBackgroundImageBrush =
            CreateImageBrushOrBlack(Settings.ChoiceBackgroundImagePath, Stretch.Fill);

        public static readonly Brush ChoiceBackgroundResultImageBrush =
            CreateImageBrushOrBlack(Settings.ChoiceBackgroundResultImagePath, Stretch.Fill);

        public static readonly Brush PlayerPlaceHolderImageBrush =
            CreateImageBrushOrBlack(Settings.PlaceholderPlayerImagePath);

        public static readonly Brush PlayerCardImageBrush =
            CreateImageBrushOrBlack(Settings.PlayerCardBackgroundImagePath);

        public static readonly Brush PlayerCardWinnerImageBrush =
            CreateImageBrushOrBlack(Settings.PlayerCardBackgroundWinnerImagePath);

        public static readonly Brush PlayGroundBackGround =
            CreateImageBrushOrBlack(Settings.BackgroundImagePath, Stretch.UniformToFill);
    }
}