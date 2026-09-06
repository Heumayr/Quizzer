using Quizzer.Base;
using Quizzer.DataModels.Models;
using Quizzer.Logic.Controller.TypedControllers;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>
    /// Eine Frage als Kopie anlegen.
    /// <para>
    /// <b>Der eigentliche Hebel für den Abend mit zehn ähnlichen Fragen.</b> Die Typmaske macht
    /// das Anlegen leichter; sie ändert aber nichts daran, dass jede Frage bei null anfängt. Wer
    /// zehn Multiple-Choice-Fragen derselben Machart anlegt, landet mit „Duplizieren" in einer
    /// Maske, in der Kategorie, Punkte, Schwierigkeit und die Antwortzeilen schon stehen.
    /// </para>
    /// </summary>
    internal partial class QuestionsViewModel
    {
        private AsyncRelayCommand? duplicateQuestionCommand;

        public ICommand DuplicateQuestionCommand => duplicateQuestionCommand
            ??= new AsyncRelayCommand(DuplicateQuestionAsync);

        private async Task DuplicateQuestionAsync(object? commandParameter)
        {
            var vorlage = commandParameter as QuestionBase ?? SelectedQuestions.FirstOrDefault();

            if (vorlage == null)
            {
                UserPrompt.Inform("Bitte zuerst eine Frage auswählen.", "Frage duplizieren");
                return;
            }

            var kopie = await LadeUndKopiereAsync(vorlage.Id);

            if (kopie == null)
                return;

            await EditQuestionAsync(kopie);
        }

        /// <summary>
        /// Lädt die Vorlage <b>über ihre Id neu</b> und gibt eine Kopie samt Schritten zurück.
        /// <para>
        /// <b>Neu laden ist nicht Vorsicht, sondern Pflicht.</b> Die Fragenliste kommt aus
        /// <c>GetAllAsync</c> und trägt keine Schritte - die Include-Kette hängt nur an
        /// <c>Get</c>. Ein Duplikat aus dem Listeneintrag wäre schrittlos, nichts stürzte ab, und
        /// es fiele am Quizabend auf. <c>EditQuestionViewModel.LoadModel</c> lädt aus genau
        /// diesem Grund schon heute per Id nach.
        /// </para>
        /// </summary>
        private static async Task<QuestionBase?> LadeUndKopiereAsync(Guid id)
        {
            using var ctrl = new QuestionBasesController();

            var vollstaendig = await ctrl.GetAsync(id);

            if (vollstaendig == null)
            {
                UserPrompt.Inform(
                    "Die Frage ließ sich nicht laden. Vielleicht wurde sie inzwischen gelöscht.",
                    "Frage duplizieren");

                return null;
            }

            // Ohne Kennung: die Kopie ist eine neue Frage und bekommt beim Speichern eine eigene
            // Id - und einen eigenen Besitzer.
            var kopie = vollstaendig.CloneWithoutReferences(copyIdentity: false);

            kopie.Designation = Kopiename(vollstaendig.Designation);
            kopie.Steps.Clear();

            foreach (var schritt in vollstaendig.Steps.OrderBy(s => s.SequenceNumber))
            {
                var geklont = schritt.CloneWithoutReferences(copyIdentity: false);

                geklont.QuestionBaseId = Guid.Empty;

                kopie.Steps.Add(geklont);
            }

            return kopie;
        }

        /// <summary>
        /// „Hauptstädte 3" wird zu „Hauptstädte 3 (Kopie)". <b>Die Kopie heißt anders</b>, sonst
        /// stehen zwei gleichnamige Fragen in der Liste und niemand weiß, welche die neue ist.
        /// </summary>
        internal static string Kopiename(string? original)
        {
            var name = (original ?? string.Empty).Trim();

            return string.IsNullOrEmpty(name) ? "Kopie" : name + " (Kopie)";
        }
    }
}
