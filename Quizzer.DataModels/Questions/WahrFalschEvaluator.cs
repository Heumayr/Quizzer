using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.DataModels.Questions
{
    /// <summary>Wie ein Spieler eine Aussage beurteilt hat.</summary>
    /// <param name="Taste">Die Taste der Aussage.</param>
    /// <param name="Text">Die Aussage.</param>
    /// <param name="IstWahr">Was stimmt.</param>
    /// <param name="GetipptWahr">Was der Spieler getippt hat.</param>
    public sealed record WahrFalschUrteil(string Taste, string Text, bool IstWahr, bool GetipptWahr)
    {
        /// <summary>Ob der Spieler richtig lag.</summary>
        public bool Richtig => IstWahr == GetipptWahr;
    }

    /// <summary>Die Auswertung einer Abgabe.</summary>
    public sealed record WahrFalschErgebnis(IReadOnlyList<WahrFalschUrteil> Urteile, int Punkte)
    {
        public int Richtige => Urteile.Count(u => u.Richtig);

        public int Gesamt => Urteile.Count;

        public bool AllesRichtig => Gesamt > 0 && Richtige == Gesamt;
    }

    /// <summary>
    /// Wertet „Wahr oder falsch" aus (M12, 2026-10-21).
    /// <para>
    /// <b>Die Abgabe enthält die Tasten der Aussagen, die der Spieler für wahr hält</b> - alle
    /// übrigen hat er als falsch beurteilt. Das Telefon gibt erst ab, wenn jede Aussage beurteilt
    /// ist; eine leere Abgabe heißt deshalb „alles falsch" und nicht „nichts getan".
    /// </para>
    /// <para>
    /// Rein rechnend, wie <see cref="ReihenfolgeEvaluator"/>; der Anteil wird genauso gerundet.
    /// </para>
    /// </summary>
    public static class WahrFalschEvaluator
    {
        /// <summary>Die Aussagen der Frage - ohne Start und Abschluss, in Tastenfolge.</summary>
        public static IReadOnlyList<QuestionStepResource> Aussagen(QuestionBase frage)
        {
            ArgumentNullException.ThrowIfNull(frage);

            return (frage.Steps ?? [])
                .Where(s => !s.IsStart && !s.IsFinish && !s.IsQuestionOnly)
                .OrderBy(s => s.QuestionViewKey, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Wertet eine Abgabe aus.</summary>
        /// <param name="frage">Die Frage - ihre Schritte tragen die Tasten aus <c>CalculateOrderdSteps</c>.</param>
        /// <param name="wahrGetippt">Die Tasten, die der Spieler als wahr beurteilt hat.</param>
        /// <param name="punkte">Was die Frage bei lauter richtigen Urteilen bringt.</param>
        public static WahrFalschErgebnis Werte(QuestionBase frage, IEnumerable<string>? wahrGetippt, int punkte)
        {
            var wahr = new HashSet<string>(wahrGetippt ?? [], StringComparer.Ordinal);

            var urteile = Aussagen(frage)
                .Select(s => new WahrFalschUrteil(
                    s.QuestionViewKey,
                    string.IsNullOrWhiteSpace(s.StepText) ? s.Designation : s.StepText,
                    s.IsResult,
                    wahr.Contains(s.QuestionViewKey)))
                .ToList();

            var richtige = urteile.Count(u => u.Richtig);

            return new WahrFalschErgebnis(urteile, ReihenfolgeEvaluator.Anteil(punkte, richtige, urteile.Count));
        }
    }
}
