using Quizzer.Base;
using Quizzer.DataModels.Anzeige;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;
using System.ComponentModel;
using System.Windows.Media;

namespace Quizzer.Views.GameViews.Sub
{
    public partial class PlayerResultContext
    {
        /// <summary>
        /// Bei welchem Team diese Karte zählt (M37 T3): der Stempel am Ergebnis, ohne Stempel das heutige Team des Spielers -
        /// dieselbe Regel wie die Teamsumme (<see cref="Teamwertung.TeamDes"/>).
        /// </summary>
        internal Guid? ZaehltFuerTeam(Game spiel) => Teamwertung.TeamDes(spiel, Result);
    }

    /// <summary>
    /// Die Spielerkarten eines Teams im Bewertungsfenster (M37 T3) mit Kopfzeile: Fläche, Symbol und Name über die Vorlage
    /// <c>TeamAnzeige</c>, dahinter was die gesetzten Bewertungen dem Team bringen - „Rot: +300 (je +100)". Die Gruppe ohne
    /// Team sagt, dass ihre Punkte für kein Team zählen.
    /// </summary>
    public sealed class TeamKartengruppe : ViewCommonBase, INotifyPropertyChanged
    {
        /// <param name="spalten">Karten je Zeile - für alle Gruppen gleich, damit eine Karte überall gleich breit ist.</param>
        public TeamKartengruppe(GameTeam? team, IReadOnlyList<PlayerResultContext> karten, int spalten)
        {
            Team = team;
            Karten = karten;
            Spalten = spalten;

            var farbe = TeamKachel.FarbeVon(team);
            Symbol = farbe?.Symbol ?? "–";
            Pinsel = TeamKachel.PinselVon(farbe);

            foreach (var karte in karten)
                karte.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(PlayerResultContext.CurrentScoreManipulation))
                        OnPropertyChanged(nameof(VorschlagText));
                };
        }

        public GameTeam? Team { get; }

        public bool IstOhneTeam => Team == null;

        public string Name => Team?.Name ?? "Ohne Team";

        public string Symbol { get; }

        public Brush Pinsel { get; }

        public IReadOnlyList<PlayerResultContext> Karten { get; }

        public int Spalten { get; }

        /// <summary>Was die gesetzten Bewertungen dem Team bringen; leer, solange keine gesetzt ist.</summary>
        public string VorschlagText
        {
            get
            {
                if (IstOhneTeam)
                    return $": {TeamKachel.OhneTeamText}";

                var gesetzt = Karten.Select(k => k.CurrentScoreManipulation).Where(w => w != 0).ToList();

                if (gesetzt.Count == 0)
                    return string.Empty;

                var je = gesetzt.Count > 1 && gesetzt.Distinct().Count() == 1 ? $" (je {Punktewechsel.Text(gesetzt[0])})" : string.Empty;
                var summe = gesetzt.Sum();
                return $": {(summe == 0 ? "0" : Punktewechsel.Text(summe))}{je}";
            }
        }
    }
}
