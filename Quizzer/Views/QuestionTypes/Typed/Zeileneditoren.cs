using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions.Schrittbau;

namespace Quizzer.Views.QuestionTypes.Typed
{
    /// <summary>
    /// Welche Maske zu welchem Fragetyp gehört - einzige Stelle, an der die Zuordnung steht.
    /// <para>
    /// Die Maske selbst sucht sich WPF über eine <c>DataTemplate</c> je ViewModel-Typ; ein
    /// <c>DataTemplateSelector</c> wie bei den Spielansichten braucht es hier nicht, weil dort
    /// <b>ein</b> Datentyp auf mehrere Vorlagen zeigt und hier jede Maske ihr eigenes ViewModel
    /// hat.
    /// </para>
    /// </summary>
    internal static class Zeileneditoren
    {
        /// <summary>Baut die Maske zu einer Frage.</summary>
        /// <param name="composer">Der Übersetzer des Typs.</param>
        /// <param name="frage">Die Frage, deren Schritte gelesen werden.</param>
        /// <param name="melde">Wird gerufen, wenn sich in der Maske etwas ändert.</param>
        internal static ZeileneditorViewModel Fuer(
            IStepComposer composer, QuestionBase frage, Action melde)
            => composer.Typ switch
            {
                QuestionType.MultipleChoice => new AntwortlisteViewModel(composer, frage, melde),
                QuestionType.Appreciate => new SollwertViewModel(composer, frage, melde),
                QuestionType.Reveal => new AufdeckViewModel(composer, frage, melde),
                _ => new HinweisleiterViewModel(composer, frage, melde),
            };
    }
}
