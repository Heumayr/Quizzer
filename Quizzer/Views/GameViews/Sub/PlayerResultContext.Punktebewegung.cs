using Quizzer.DataModels.Helpers;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Punktebewegungen auf der Spielerkarte (M35 B1): ein Minispiel oder eine Sonderregel legt einen
    /// Vorschlag mit Grund ab, gebucht wird erst mit der Bestaetigung des Spielleiters (E1).
    /// </summary>
    public partial class PlayerResultContext
    {
        /// <summary>Warum der Vorschlag so lautet, etwa „Klau durch Anna"; leer bei eigener Bewertung.</summary>
        public string? Grund { get; private set; }

        private string GrundText => Grund is { Length: > 0 } grund ? $" ({grund})" : string.Empty;

        /// <summary>Was der Spieler im Spiel schon gebucht hat, auch in dieser Zelle.</summary>
        public int Kontostand => Coordinate?.Game is { } spiel && Player != null ? spiel.Kontostand(Player.Id) : 0;

        private bool UnterNullErlaubt => Coordinate?.Game?.AllowNegativeScore ?? true;

        /// <summary>Kappt einen Minus-Vorschlag am Kontostand, wenn das Spiel kein Minus erlaubt (E2).</summary>
        private int KappeMinus(int vorschlag) => Punktebewegung.Kappe(vorschlag, Kontostand, UnterNullErlaubt);

        /// <summary>Legt eine Punktebewegung als „eigener Wert" samt Grund auf die Karte, noch nicht gebucht.</summary>
        public void SchlageVor(int punkte, string grund)
        {
            Suggestion = ScoreSuggestion.Correction;
            CurrentScoreManipulation = KappeMinus(punkte);
            Grund = grund;
            OnPropertyChanged(nameof(Grund));
            OnPropertyChanged(nameof(SuggestionText));
        }
    }
}
