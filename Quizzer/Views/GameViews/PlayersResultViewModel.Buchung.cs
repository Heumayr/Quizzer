using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Logic.Spielablauf;
using Quizzer.Views.GameViews.Sub;

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
        {
            beimOeffnen = Coordinate is { } zelle ? LetzteWertung.Merke(zelle.Game, zelle, Results) : null;
            MerkeGespeicherteHaekchen();
        }

        /// <summary>
        /// Was gerade speichert (P10-6): das Schließen wartet darauf. Sonst schrieb es dieselben Zeilen ein zweites Mal - das
        /// Häkchen gilt bis zum Ende des ersten als offen - und scheiterte an deren RowVersion.
        /// </summary>
        private Task laufendesSpeichern = Task.CompletedTask;

        /// <inheritdoc cref="SpeichereAsync"/>
        public Task<bool> TrySaveAsync()
        {
            var speichern = SpeichereAsync();
            laufendesSpeichern = speichern;
            return speichern;
        }

        /// <summary>
        /// „Speichern und Zelle abschließen“, solange es läuft (P11-1): ob abgeschlossen wurde, steht erst danach fest. Das
        /// Fragefenster wartet darauf, bevor es die Zelle als erledigt speichert oder den Beamer räumt.
        /// </summary>
        internal Task LaufendesAbschliessen { get; private set; } = Task.CompletedTask;

        /// <summary>Beim Öffnen und nach jedem gelungenen Speichern: das Häkchen ist ab hier nicht mehr offen (P10-1).</summary>
        private void MerkeGespeicherteHaekchen()
        {
            foreach (var karte in PlayerResultContextList)
                karte.MerkeGespeichertesHaekchen();
        }

        /// <summary>Ob seit dem Öffnen an einer Zeile dieser Zelle gebucht wurde.</summary>
        public bool HatGebucht => beimOeffnen?.HatGebucht(Results) == true;

        /// <summary>
        /// V26: kommt eine Neuwertung nach der Buchung (Staffel: „Antwort gelten lassen“ nach dem Ende), steht nur der Unterschied zum
        /// seit dem Öffnen Gebuchten auf der Karte - als Vorschlag (E1), nie als zweite volle Buchung. Vor der Buchung unverändert.
        /// </summary>
        internal Punktevorschlag? AbzueglichGebucht(PlayerResultContext karte, Punktevorschlag? vorschlag)
        {
            if (beimOeffnen is not { } stand || !HatGebucht)
                return vorschlag;

            var unterschied = (vorschlag?.Punkte ?? 0) - stand.GebuchtSeitDemMerken(karte.Result);
            var grund = vorschlag is null ? "Neu gewertet – der gebuchte Anteil geht zurück" : $"Neu gewertet – {vorschlag.Grund}";

            return unterschied == 0 ? null : new Punktevorschlag(unterschied, grund);
        }
    }
}
