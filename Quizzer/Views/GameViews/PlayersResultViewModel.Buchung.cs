using Quizzer.Logic.Spielablauf;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Was seit dem Öffnen der Zelle gebucht ist (Durchsicht M37, R5-1). Gebucht wird beim Speichern und beim Schließen
    /// (Live-Speichern 2026-10-22); danach steht kein Vorschlag mehr auf der Karte, und wer nur nach offenen Bewertungen fragt,
    /// sieht nichts mehr. Der Stand vom Öffnen sagt es trotzdem.
    /// </summary>
    public partial class PlayersResultViewModel
    {
        private LetzteWertung? beimOeffnen;

        /// <summary>Aus <see cref="SetCoordinateAsync"/>: die frisch gelesenen Zeilen sind der Stand vom Öffnen.</summary>
        private void MerkeStandBeimOeffnen()
            => beimOeffnen = Coordinate is { } zelle ? LetzteWertung.Merke(zelle.Game, zelle, Results) : null;

        /// <summary>Ob seit dem Öffnen an einer Zeile dieser Zelle gebucht wurde.</summary>
        public bool HatGebucht => beimOeffnen?.HatGebucht(Results) == true;
    }
}
