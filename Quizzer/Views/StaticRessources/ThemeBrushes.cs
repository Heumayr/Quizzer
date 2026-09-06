using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Themes;
using System.Collections.Concurrent;
using System.Windows.Media;

namespace Quizzer.Views.StaticRessources
{
    /// <summary>
    /// Die Texturen eines Designs, als fertige Pinsel.
    /// <para>
    /// <b>Warum es das gibt:</b> <see cref="StaticResources"/> laedt seine Pinsel einmal beim
    /// ersten Zugriff und friert sie ein. Damit ist das Aussehen fuer die Laufzeit des Programms
    /// festgelegt - ein Design je Spiel waere unmoeglich. Diese Klasse haelt dieselben Pinsel,
    /// aber je Design, und traegt sie in einem Zwischenspeicher, damit ein Bild nicht bei jedem
    /// Zellenaufbau neu von der Platte kommt.
    /// </para>
    /// <para>
    /// <b>Der Auslieferungsstand bleibt der Auslieferungsstand.</b> Ein Spiel ohne Design
    /// bekommt <see cref="Standard"/>, und das reicht die Pinsel von
    /// <see cref="StaticResources"/> unveraendert durch. Wer nichts einstellt, sieht nichts
    /// Neues.
    /// </para>
    /// </summary>
    public sealed class ThemeBrushes
    {
        private static readonly ConcurrentDictionary<string, ThemeBrushes> Zwischenspeicher = new();

        private readonly string ordner;

        private ThemeBrushes(string ordner, GameTheme? theme)
        {
            this.ordner = ordner;
            Theme = theme;

            Hintergrund = Lade("Background.png", Stretch.UniformToFill, StaticResources.PlayGroundBackGround);
            Zelle = Lade("CellBackground.png", Stretch.Fill, StaticResources.CellImageBrush);
            ZelleHover = Lade("CellBackgroundHover.png", Stretch.Fill, StaticResources.CellHoverImageBrush);
            ZelleGespielt = Lade("CellBackgroundIsDone.png", Stretch.Fill, StaticResources.CellImageBrushIsDone);
            SpaltenKopf = Lade("HeaderColumnBackground.png", Stretch.Fill, StaticResources.HeaderColumnImageBrush);
            ZeilenKopf = Lade("HeaderRowBackground.png", Stretch.Fill, StaticResources.HeaderRowImageBrush);
            Auswahlfeld = Lade("GridBackground.png", Stretch.Fill, StaticResources.ChoiceBackgroundImageBrush);
            AuswahlfeldRichtig = Lade("GridBackgroundResult.png", Stretch.Fill, StaticResources.ChoiceBackgroundResultImageBrush);
            Schrittleiste = Lade("HorizontalBackground.png", Stretch.Fill, StaticResources.HorizontalBackgroundImageBrush);
            Spielerkarte = Lade("PlayerCardBackground.png", Stretch.Fill, StaticResources.PlayerCardImageBrush);
            SpielerkarteSieger = Lade("PlayerCardBackgroundWinner.png", Stretch.Fill, StaticResources.PlayerCardWinnerImageBrush);
            SpielerPlatzhalter = Lade("PlaceholderPlayer.png", Stretch.Fill, StaticResources.PlayerPlaceHolderImageBrush);
        }

        /// <summary>Das Design, aus dem diese Pinsel stammen; <c>null</c> beim Auslieferungsstand.</summary>
        public GameTheme? Theme { get; }

        public Brush Hintergrund { get; }
        public Brush Zelle { get; }
        public Brush ZelleHover { get; }
        public Brush ZelleGespielt { get; }
        public Brush SpaltenKopf { get; }
        public Brush ZeilenKopf { get; }
        public Brush Auswahlfeld { get; }
        public Brush AuswahlfeldRichtig { get; }
        public Brush Schrittleiste { get; }
        public Brush Spielerkarte { get; }
        public Brush SpielerkarteSieger { get; }
        public Brush SpielerPlatzhalter { get; }

        /// <summary>Der Auslieferungsstand: genau die Pinsel, die es immer gab.</summary>
        public static ThemeBrushes Standard { get; } = new(string.Empty, null);

        /// <summary>
        /// Das Design, mit dem gerade gezeichnet wird. Wer ein Spiel oeffnet, setzt es; der
        /// Spielaufbau setzt es beim Wechsel der Auswahl, damit die Vorschau stimmt.
        /// </summary>
        public static ThemeBrushes Current { get; private set; } = Standard;

        /// <summary>Liefert die Pinsel zu einem Design, aus dem Zwischenspeicher.</summary>
        public static ThemeBrushes For(GameTheme? theme)
        {
            if (theme == null || string.IsNullOrWhiteSpace(theme.FolderName))
                return Standard;

            return Zwischenspeicher.GetOrAdd(theme.FolderName, _ => new ThemeBrushes(theme.FolderName, theme));
        }

        /// <summary>
        /// Stellt das Programm auf ein Design um und sagt, ob sich dabei etwas geaendert hat.
        /// Der Rueckgabewert erspart dem Aufrufer ein ueberfluessiges Neuzeichnen.
        /// </summary>
        public static bool SetCurrent(GameTheme? theme)
        {
            var neu = For(theme);

            if (ReferenceEquals(neu, Current))
                return false;

            Current = neu;

            return true;
        }

        /// <summary>
        /// Wirft den Zwischenspeicher weg. Noetig, nachdem im Design-Editor eine Textur
        /// ausgetauscht wurde - sonst zeigt die Vorschau das alte Bild weiter.
        /// </summary>
        public static void Forget()
        {
            Zwischenspeicher.Clear();
            Current = Standard;
        }

        private Brush Lade(string dateiname, Stretch stretch, Brush rueckfall)
        {
            if (!ThemeAssets.IsOwn(ordner, dateiname))
                return rueckfall;

            var pfad = ThemeAssets.Resolve(ordner, dateiname);

            return StaticResources.CreateImageBrushOrFallback(pfad, stretch, rueckfall);
        }
    }
}
