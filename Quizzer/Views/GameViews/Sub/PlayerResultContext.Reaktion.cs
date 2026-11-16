using Quizzer.DataModels.Questions.Minispiele;
using System.Windows;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Reaktion auf der Spielerkarte (M35 B3): die beste Zeit des Spielers und der Vorschlag. Gerechnet
    /// wird ueber alle Karten in <c>PlayersResultViewModel.WerteReaktion</c>, hier nur uebernommen.
    /// </summary>
    public partial class PlayerResultContext
    {
        /// <summary>Uebernimmt das Ergebnis der Reaktion: beste Zeit „richtig" mit den Feldpunkten, sonst nichts.</summary>
        internal void UebernimmReaktion(ReaktionErgebnis ergebnis)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            istZuordnung = false;
            Reihenfolge = null;

            var gewinner = ergebnis.IstGewinner(Player.Id);

            // Nie allein ueber Farbe: der Satz sagt, ob die Zeit gewinnt.
            var satz = ergebnis.BestzeitVon(Player.Id) is { } ms
                ? $"Beste Zeit: {ms} ms – " + (gewinner ? "die schnellste" : "nicht die schnellste")
                : ergebnis.Abgegeben.Contains(Player.Id)
                    ? "Keine gültige Zeit (Frühstart oder zu langsam)"
                    : "Keine Abgabe – von Hand werten";

            ZeigeMehrheit([], Visibility.Collapsed, satz);

            // Ohne gueltige Zeit kein Vorschlag (E1) - ein getrenntes Telefon wertet der Spielleiter.
            WechsleVorschlag(gewinner ? true : null);
        }
    }
}
