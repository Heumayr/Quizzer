using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.Views.GameViews.Teamregeln
{
    /// <summary>
    /// Teamregeln, bei denen jedes Mitglied selbst antwortet (M37 T6): „Gleich gedacht" und „Alle oder keiner". Es gibt keinen
    /// Sprecher dieser Frage; antworten dürfen alle Mitglieder, die nicht ausgelassen sind (Joker-Sperre, getrennt) - auch wenn
    /// keiner bleibt, antwortet niemand sonst (fail-closed). Die Telefone zeigen weiter den Sprecher des Stands.
    /// </summary>
    internal abstract class JederAntwortetAblauf : TeamregelAblauf
    {
        protected JederAntwortetAblauf(CellSpecialRule regel)
            : base(regel)
        {
        }

        public override IReadOnlyCollection<Guid>? Antwortende
            => Teams.SelectMany(t => t.Folge).Where(m => !Ausgelassen.ContainsKey(m)).Distinct().ToList();

        /// <summary>Nur wer per Joker gesperrt ist, liest einen eigenen Satz; ein getrenntes Telefon sagt es allgemein.</summary>
        public override IReadOnlyDictionary<Guid, string>? Sperrsaetze
        {
            get
            {
                var mitglieder = Teams.SelectMany(t => t.Folge).ToHashSet();

                return Ausgelassen.Where(e => e.Value != null && mitglieder.Contains(e.Key)).ToDictionary(e => e.Key, e => e.Value!);
            }
        }

        public override IReadOnlyDictionary<Guid, Guid>? SprecherAmTelefon => null;

        public override string Standtext => "Jeder antwortet für sich";

        protected override string AbgabeText(Guid spieler)
            => Teams.FirstOrDefault(t => t.Folge.Contains(spieler)) is not { } team ? string.Empty
                : Ausgelassen.ContainsKey(spieler) ? "Antwortet diesmal nicht"
                : $"Antwortet für {team.Name}";

        /// <summary>Die Mitglieder des Teams, die eine Karte haben, in Sprecherfolge - über sie wird aufgeteilt.</summary>
        protected static IReadOnlyList<Guid> MitKarte(Teamaufstellung team, IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
            => team.Folge.Where(eingaenge.ContainsKey).ToList();
    }
}
