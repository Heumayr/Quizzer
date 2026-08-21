using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.Logic.Controller.TypedControllers
{
    public partial class QuestionBasesController
    {
        /// <summary>
        /// Speichert die Frage samt ihren Schritten.
        /// <para>
        /// <see cref="GenericController{TEntity}.UpsertAsync(TEntity, System.Func{TEntity, bool}?)"/>
        /// schreibt die Schritte nicht mit: <c>CopyQuestionBaseValuesTo</c> leert die Liste im
        /// Klon, und genau dieser Klon geht an EF. Bis hierher hat der Editor das dadurch
        /// umgangen, dass er die Frage vor jedem Schritt-Dialog vorab gespeichert hat.
        /// </para>
        /// <para>
        /// Bewusst eine eigene Methode und nicht in <c>UpsertAsync</c> eingebaut: die Fragenliste
        /// laedt ihre Fragen ueber <c>GetAllAsync</c> - und das laedt die Schritte nicht mit.
        /// Ein Gleichzug in <c>UpsertAsync</c> wuerde dort jeder Frage saemtliche Schritte
        /// loeschen, weil die Liste leer aussieht.
        /// </para>
        /// </summary>
        /// <param name="question">Die Frage mit dem vollstaendigen Stand ihrer Schritte.</param>
        /// <returns>Die gespeicherte Frage.</returns>
        public async Task<QuestionBase> SaveWithStepsAsync(QuestionBase question)
        {
            ArgumentNullException.ThrowIfNull(question);

            var steps = question.Steps?.ToList() ?? new List<QuestionStepResource>();

            var result = await UpsertAsync(question).ConfigureAwait(false);
            var questionId = result.Entity.Id;

            using var stepController = new QuestionStepResourcesController(this);

            var storedIds = await stepController.EntitySet
                .Where(s => s.QuestionBaseId == questionId)
                .Select(s => s.Id)
                .ToListAsync()
                .ConfigureAwait(false);

            foreach (var step in steps)
            {
                step.QuestionBaseId = questionId;
                await stepController.UpsertAsync(step).ConfigureAwait(false);
            }

            // Was der Spielleiter entfernt hat, faellt hier weg - deshalb darf diese Methode nur
            // mit einem vollstaendig geladenen Schrittstand aufgerufen werden.
            var keptIds = steps.Select(s => s.Id).ToHashSet();

            foreach (var removedId in storedIds.Where(id => !keptIds.Contains(id)))
            {
                await stepController.DeleteAsync(removedId).ConfigureAwait(false);
            }

            question.Steps = steps;

            return result.Entity;
        }
    }
}
