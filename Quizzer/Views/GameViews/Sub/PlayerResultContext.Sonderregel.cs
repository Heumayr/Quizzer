using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;
using System.Windows;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Sonderregel auf der Spielerkarte (M35 C3): die eigene Bewertung geht an die Regel, die Regel rechnet
    /// ueber <b>alle</b> Karten der Zelle (Wette, Kopfgeld haengen an fremden Bewertungen) und legt je Karte
    /// einen Vorschlag mit Grund ab (E1), gekappt (E2). Ein eigener Wert von Hand bleibt stehen.
    /// </summary>
    public partial class PlayerResultContext
    {
        /// <summary>Was dieser Spieler in der Vorphase abgegeben hat, etwa „Einsatz: 200"; leer ohne Regel.</summary>
        public string SonderregelText => Player == null ? string.Empty : CurrentQuestionViewModel?.SonderregelAbgabe(Player.Id) ?? string.Empty;

        public Visibility SonderregelVisibility => SonderregelText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        private RatingKind regelBewertung;
        private int regelGrundwert;

        /// <summary>Was die Regel zuletzt vorschlug - nur ein Wechsel setzt neu, wie bei den Minispielen (E1).</summary>
        private Punktevorschlag? regelVorschlag;

        /// <summary>Die Karte war unbewertet und traegt nur die Bewegung der Regel - als eigener Wert gebucht.</summary>
        private bool regelKorrektur;

        /// <summary>Beim Buchen setzt die Karte zurueck, ohne die Zelle neu zu werten (die Kontostaende wandern dabei).</summary>
        private bool bucht;

        /// <summary>
        /// R4-5: „Ohne Wertung" von Hand (Knopf, Strg+0) an einer Teamregel ist eine Entscheidung der Spielleitung - der Anteil des
        /// Teams kommt nicht wieder auf die Karte, bis sie die Karte selbst bewertet. Kopfgeld und Wette (M35 C3) bleiben, wie sie
        /// sind: dort ist „unbewertet" der Normalfall, und ein Kopfgeld wanderte sonst nur zur einen Seite.
        /// </summary>
        private bool ohneWertungVonHand;

        private void SetzeOhneWertungVonHand()
        {
            ohneWertungVonHand = true;
            Suggestion = ScoreSuggestion.None;
        }

        /// <summary>Eine neue Runde („Runde zurücksetzen“): die Entscheidung galt den alten Antworten.</summary>
        internal void VergissOhneWertungVonHand() => ohneWertungVonHand = false;

        /// <summary>Die eigene Bewertung, wie die Regel sie sieht; ein eigener Wert von Hand gilt als nicht bewertet.</summary>
        internal SonderregelEingang SonderregelEingang
            => new(regelBewertung, regelGrundwert, GetCurrentPoints(), new Kappung(Kontostand, UnterNullErlaubt));

        /// <summary>Die Vorphase ist zu oder die Regel ist gesetzt - die Karte zeigt die Abgabe.</summary>
        internal void SonderregelGeaendert()
        {
            OnPropertyChanged(nameof(SonderregelText));
            OnPropertyChanged(nameof(SonderregelVisibility));
        }

        /// <summary>Ein Teilwert der Typen mit Anteil - die Regel rechnet ihn um.</summary>
        private void SetzeTeilwert(int punkte)
        {
            CurrentScoreManipulation = punkte;
            MerkeBewertung(RatingKind.Partial, punkte);
        }

        /// <summary>Aus dem Setter von <see cref="Suggestion"/>: richtig, falsch oder nichts geht an die Regel.</summary>
        private void WendeSonderregelAufBewertungAn(ScoreSuggestion bewertung)
        {
            regelKorrektur = false;

            if (bewertung != ScoreSuggestion.None)
                ohneWertungVonHand = false;

            if (bewertung == ScoreSuggestion.Right)
                MerkeBewertung(RatingKind.Right, GetCurrentPoints());
            else if (bewertung == ScoreSuggestion.Wrong)
                MerkeBewertung(RatingKind.Wrong, -(Coordinate?.CurrentMinusPoints ?? 0));
            else
                MerkeBewertung(RatingKind.None, 0);
        }

        private void MerkeBewertung(RatingKind bewertung, int grundwert)
        {
            regelBewertung = bewertung;
            regelGrundwert = grundwert;

            // Der Setter hat den Wert eben zurueckgesetzt - die Regel muss ihn neu setzen duerfen.
            regelVorschlag = null;

            if (!bucht)
                CurrentQuestionViewModel?.WerteSonderregel();
        }

        /// <summary>Der Vorschlag der Regel fuer diese Karte; <c>null</c>: die Regel greift nicht, es gilt die Bewertung.</summary>
        internal void UebernimmSonderregel(Punktevorschlag? vorschlag)
        {
            if (vorschlag == regelVorschlag)
                return;

            regelVorschlag = vorschlag;

            if (ohneWertungVonHand && suggestion == ScoreSuggestion.None && CurrentQuestionViewModel?.TeamregelGespielt == true)
                return;

            // Ein eigener Wert von Hand bleibt, wie der Spielleiter ihn eintraegt (E1).
            if (suggestion == ScoreSuggestion.Correction && !regelKorrektur && regelBewertung != RatingKind.Partial)
                return;

            if (vorschlag == null)
            {
                SetzeBewertungswertZurueck();
                return;
            }

            if (suggestion == ScoreSuggestion.None)
                WechsleSuggestionStill(ScoreSuggestion.Correction, alsRegelKorrektur: true);

            CurrentScoreManipulation = vorschlag.Punkte;
            ZeigeGrund(vorschlag.Grund);
        }

        /// <summary>Die Regel greift nicht mehr: zurueck auf den Wert der Bewertung selbst.</summary>
        private void SetzeBewertungswertZurueck()
        {
            if (regelKorrektur)
                WechsleSuggestionStill(ScoreSuggestion.None, alsRegelKorrektur: false);

            CurrentScoreManipulation = suggestion switch
            {
                ScoreSuggestion.Right or ScoreSuggestion.Correction => regelGrundwert,
                ScoreSuggestion.Wrong => KappeMinus(regelGrundwert),
                _ => 0,
            };
            ZeigeGrund(null);
        }

        /// <summary>Setzt die Bewertung ohne den Setter - der setzte den Wert zurueck und wertete die Zelle neu.</summary>
        private void WechsleSuggestionStill(ScoreSuggestion neu, bool alsRegelKorrektur)
        {
            suggestion = neu;
            regelKorrektur = alsRegelKorrektur;
            OnPropertyChanged(nameof(Suggestion));
            OnPropertyChanged(nameof(CanNotEditManipulation));
        }

        private void ZeigeGrund(string? grund)
        {
            Grund = grund;
            OnPropertyChanged(nameof(Grund));
            OnPropertyChanged(nameof(SuggestionText));
        }
    }
}
