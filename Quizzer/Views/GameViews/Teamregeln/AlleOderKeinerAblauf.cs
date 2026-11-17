using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.Views.GameViews.Teamregeln
{
    /// <summary>
    /// „Alle oder keiner" (M37 T6, F-M37-02): jedes Mitglied antwortet selbst, die Spielleitung bewertet jede Karte (E1). Sind
    /// alle richtig, die antworten konnten, bekommt das Team den Feldwert, ist einer falsch, verliert es das Minus - beides
    /// einmal, über das ganze Team aufgeteilt (<see cref="AlleOderKeiner"/>). Solange jemand unbewertet ist, trägt eine schon
    /// richtig bewertete Karte 0 mit dem Grund, damit dort nicht die vollen Feldpunkte stehen.
    /// </summary>
    internal sealed class AlleOderKeinerAblauf : JederAntwortetAblauf
    {
        /// <summary>Platzhalter der Texteingabe am Telefon.</summary>
        internal const string Eingabe = "Deine Antwort – dein ganzes Team muss richtig liegen";

        public AlleOderKeinerAblauf()
            : base(CellSpecialRule.AllOrNothing)
        {
        }

        public override string Eingabehinweis => Eingabe;

        public override string Hinweis => Teams.Count == 0 ? string.Empty : "Jeder antwortet für sich – Punkte nur, wenn das ganze Team richtig liegt";

        protected override IReadOnlyDictionary<Guid, Teamanteil> Anteile(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
        {
            var anteile = new Dictionary<Guid, Teamanteil>();
            var ausgelassen = Ausgelassen.Keys.ToHashSet();

            foreach (var team in Teams)
            {
                var folge = MitKarte(team, eingaenge);

                if (folge.Count == 0)
                    continue;

                var feld = eingaenge[folge[0]].Feldpunkte;
                var bewertungen = folge.ToDictionary(m => m, m => eingaenge[m].Bewertung);

                var ergebnis = AlleOderKeiner.Werte(feld, Grundlage.Minuspunkte, folge, ausgelassen, bewertungen);

                // Offen, oder niemand konnte antworten (fail-closed): eine schon bewertete Karte trägt 0 statt ihres eigenen Werts.
                if (ergebnis is not { Bewertung: not RatingKind.None })
                {
                    var grund = ergebnis == null ? "niemand konnte antworten" : $"noch {ergebnis.Offen} unbewertet";

                    foreach (var mitglied in folge.Where(m => eingaenge[m].Grundwert != 0))
                        anteile[mitglied] = new Teamanteil(RatingKind.None, 0, $"Alle oder keiner {team.Name}: {grund}");

                    continue;
                }

                var wertung = ergebnis.Bewertung == RatingKind.Right
                    ? $"Alle oder keiner {team.Name}: alle richtig – Anteil an {Punkteanteil.Text(feld)}"
                    : $"Alle oder keiner {team.Name}: nicht alle richtig – Anteil an {Punkteanteil.Text(-Grundlage.Minuspunkte)}";

                foreach (var (mitglied, punkte) in ergebnis.Anteile)
                    anteile[mitglied] = new Teamanteil(ergebnis.Bewertung, punkte, wertung);
            }

            return anteile;
        }
    }
}
