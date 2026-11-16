using Quizzer.DataModels.Questions.Minispiele;
using System.Windows;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Tippsprint und Stoppuhr auf der Spielerkarte (M35 C1): der eigene Wert im Satz und der Vorschlag.
    /// Gerechnet wird ueber alle Karten im <c>PlayersResultViewModel</c>, hier nur uebernommen.
    /// </summary>
    public partial class PlayerResultContext
    {
        /// <summary>Meiste Tipps „richtig" mit den Feldpunkten, sonst nichts (E1).</summary>
        internal void UebernimmTippsprint(TippsprintErgebnis ergebnis)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            var gewinner = ergebnis.IstGewinner(Player.Id);

            // Nie allein ueber Farbe: der Satz sagt, ob der Wert gewinnt.
            var satz = ergebnis.TippsVon(Player.Id) is { } tipps
                ? $"{TippsprintEvaluator.Describe(tipps)} – " + (gewinner ? "die meisten" : "nicht die meisten")
                : "Keine Abgabe – von Hand werten";

            UebernimmZeitspiel(satz, gewinner);
        }

        /// <summary>Kleinster Abstand zur Zielzeit „richtig" mit den Feldpunkten, sonst nichts (E1).</summary>
        internal void UebernimmStoppuhr(StoppuhrErgebnis ergebnis)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            var gewinner = ergebnis.IstGewinner(Player.Id);
            var versuch = ergebnis.VersuchVon(Player.Id);

            var satz = versuch == null
                ? "Keine Abgabe – von Hand werten"
                : ergebnis.AbstandVon(Player.Id) is { } abstand
                    ? $"Gestoppt bei {StoppuhrEvaluator.Sekunden(versuch.Millisekunden)}, "
                      + $"{StoppuhrEvaluator.DescribeDistance(abstand)} – "
                      + (gewinner ? "am nächsten dran" : "nicht am nächsten dran")
                    : "Zu spät gestoppt – keine Wertung";

            UebernimmZeitspiel(satz, gewinner);
        }

        private void UebernimmZeitspiel(string satz, bool gewinner)
        {
            istZuordnung = false;
            Reihenfolge = null;

            ZeigeMehrheit([], Visibility.Collapsed, satz);

            // Ohne Sieg kein Vorschlag (E1) - ein getrenntes Telefon wertet der Spielleiter.
            WechsleVorschlag(gewinner ? true : null);
        }
    }
}
