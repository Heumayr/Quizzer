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

        /// <summary>
        /// Ersatzfarben, wo ein Hintergrundbild fehlt (2026-10-22, gesehen auf den ersten
        /// Bildschirmfotos der CI). Bis hierher fiel <b>jedes</b> fehlende Bild auf Schwarz - auf
        /// einem frischen Rechner war damit das Feld der richtigen Antwort so schwarz wie alle
        /// anderen, die Auflösung einer Multiple-Choice-Frage zeigte nichts, die Zellen des
        /// Spielfelds verschwanden im Hintergrund, und der schwarze Streifen der belgischen Flagge
        /// mit ihnen. Gedeckte Farben, die auf dem Beamer ruhig bleiben, aber unterscheidbar sind.
        /// </summary>
        internal static class Ersatz
        {
            public static readonly Brush Zelle = Farbe(0x1B, 0x2B, 0x44);
            public static readonly Brush ZelleHover = Farbe(0x26, 0x41, 0x6A);
            public static readonly Brush ZelleGespielt = Farbe(0x1A, 0x1A, 0x1A);
            public static readonly Brush Kopf = Farbe(0x2E, 0x2E, 0x2E);
            public static readonly Brush Auswahlfeld = Farbe(0x1C, 0x26, 0x33);
            public static readonly Brush AuswahlfeldRichtig = Farbe(0x1F, 0x5C, 0x2E);
            public static readonly Brush Schrittleiste = Farbe(0x1C, 0x1C, 0x1C);
            public static readonly Brush Platzhalter = Farbe(0x33, 0x33, 0x33);
            public static readonly Brush Spielerkarte = Farbe(0x1E, 0x1E, 0x1E);
            public static readonly Brush SpielerkarteSieger = Farbe(0x5A, 0x46, 0x10);

            private static Brush Farbe(byte r, byte g, byte b)
            {
                var pinsel = new SolidColorBrush(Color.FromRgb(r, g, b));
                pinsel.Freeze();
                return pinsel;
            }
        }

        public static readonly Brush CellImageBrush =
            CreateImageBrushOrFallback(Settings.CellBackgroundImagePath, Stretch.Fill, Ersatz.Zelle);

        public static readonly Brush CellHoverImageBrush =
            CreateImageBrushOrFallback(Settings.CellBackgroundHoverImagePath, Stretch.Fill, Ersatz.ZelleHover);

        public static readonly Brush CellImageBrushIsDone =
            CreateImageBrushOrFallback(Settings.CellBackgroundIsDoneImagePath, Stretch.Fill, Ersatz.ZelleGespielt);

        public static readonly Brush HeaderColumnImageBrush =
            CreateImageBrushOrFallback(Settings.HeaderColumnBackgroundImagePath, Stretch.Fill, Ersatz.Kopf);

        public static readonly Brush HeaderRowImageBrush =
            CreateImageBrushOrFallback(Settings.HeaderRowBackgroundImagePath, Stretch.Fill, Ersatz.Kopf);

        public static readonly Brush ChoiceBackgroundImageBrush =
            CreateImageBrushOrFallback(Settings.GridBackgroundImagePath, Stretch.Fill, Ersatz.Auswahlfeld);

        public static readonly Brush ChoiceBackgroundResultImageBrush =
            CreateImageBrushOrFallback(Settings.GridBackgroundResultImagePath, Stretch.Fill, Ersatz.AuswahlfeldRichtig);

        public static readonly Brush HorizontalBackgroundImageBrush =
            CreateImageBrushOrFallback(Settings.HorizontalBackgroundImagePath, Stretch.Fill, Ersatz.Schrittleiste);

        public static readonly Brush PlayerPlaceHolderImageBrush =
            CreateImageBrushOrFallback(Settings.PlaceholderPlayerImagePath, Stretch.Fill, Ersatz.Platzhalter);

        public static readonly Brush PlayerCardImageBrush =
            CreateImageBrushOrFallback(Settings.PlayerCardBackgroundImagePath, Stretch.Fill, Ersatz.Spielerkarte);

        public static readonly Brush PlayerCardWinnerImageBrush =
            CreateImageBrushOrFallback(Settings.PlayerCardBackgroundWinnerImagePath, Stretch.Fill, Ersatz.SpielerkarteSieger);

        public static readonly Brush PlayGroundBackGround =
            CreateImageBrushOrBlack(Settings.BackgroundImagePath, Stretch.UniformToFill);
    }
}