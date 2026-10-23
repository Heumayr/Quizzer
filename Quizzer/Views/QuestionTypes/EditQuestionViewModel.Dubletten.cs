using Quizzer.DataModels.Questions;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Dubletten (M32, 2026-10-23): ähnelt der Fragetext einer vorhandenen Frage, steht eine
    /// <b>Warnung</b> in der Liste unter dem Editor - schon beim Tippen, nicht erst beim Speichern.
    /// <para>
    /// <b>Warum keine Rückfrage beim Speichern:</b> sie hielte „Speichern und nächste" bei jeder
    /// Ähnlichkeit an, und in den Tests stünde ein echtes Fenster offen. Gesperrt wird nichts -
    /// zwei Fragen zum selben Ding können gewollt sein (leicht und schwer).
    /// </para>
    /// </summary>
    public partial class EditQuestionViewModel
    {
        public const string DuplicateCode = "possible-duplicate";

        private List<FragenAehnlichkeit.Kandidat> vergleich = new();

        private async Task LadeVergleichAsync()
        {
            using var ctrl = new QuestionBasesController();
            var alle = await ctrl.GetAllAsync();

            vergleich = DataModels.QuestionOwnership.VisibleTo(alle)
                .Select(q => new FragenAehnlichkeit.Kandidat(q.Id, q.QuestionText, q.Designation))
                .ToList();
        }

        /// <summary>Nach dem Speichern: die Frage ist ab jetzt selbst Vergleich für die nächste.</summary>
        private void MerkeFuerVergleich()
        {
            if (Question == null || Question.Id == Guid.Empty)
                return;

            vergleich.RemoveAll(k => k.Id == Question.Id);
            vergleich.Add(new(Question.Id, Question.QuestionText, Question.Designation));
        }

        private IEnumerable<ValidationIssue> Dubletten()
        {
            if (Question == null)
                yield break;

            foreach (var (frage, wert) in FragenAehnlichkeit.Finde(Question.QuestionText, vergleich, Question.Id))
            {
                yield return new ValidationIssue(DuplicateCode, ValidationSeverity.Warning,
                    $"Ähnliche Frage schon vorhanden ({wert:P0}): „{frage.Bezeichnung}“ - {Kuerze(frage.Fragetext, 90)}",
                    nameof(Question.QuestionText));
            }
        }
    }
}
