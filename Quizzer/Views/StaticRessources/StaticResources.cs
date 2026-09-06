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
            => CreateImageBrushOrFallback(path, stretch, Brushes.Black);

        /// <summary>
        /// Laedt ein Bild als Pinsel; kommt es nicht zustande, bleibt es beim Rueckfall.
        /// <para>
        /// Der Rueckfall ist der Grund fuer diese Ueberladung: <see cref="ThemeBrushes"/> laedt
        /// damit nur, was ein Design wirklich mitbringt, und faellt sonst auf den
        /// Auslieferungsstand zurueck statt auf Schwarz.
        /// </para>
        /// </summary>
        internal static Brush CreateImageBrushOrFallback(string? path, Stretch stretch, Brush fallback)
        {
            if (string.IsNullOrWhiteSpace(path))
                return fallback;

            try
            {
                if (!Uri.TryCreate(path, UriKind.Absolute, out var uri))
                    return fallback;

                if (uri.IsFile && !File.Exists(uri.LocalPath))
                    return fallback;

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
                return fallback;
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
            CreateImageBrushOrBlack(Settings.GridBackgroundImagePath, Stretch.Fill);

        public static readonly Brush ChoiceBackgroundResultImageBrush =
            CreateImageBrushOrBlack(Settings.GridBackgroundResultImagePath, Stretch.Fill);

        public static readonly Brush HorizontalBackgroundImageBrush =
            CreateImageBrushOrBlack(Settings.HorizontalBackgroundImagePath, Stretch.Fill);

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