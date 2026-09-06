using Quizzer.DataModels.Questions.Schrittbau;
using Quizzer.Views.QuestionTypes.Typed;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Die Brücke zwischen der Schale und der Maske des Fragetyps.
    /// <para>
    /// <b>Nutzerentscheidung vom 2026-09-06:</b> jeder Fragetyp bekommt eine eigene Maske. Das
    /// Schrittmodell ist für alle fünf dasselbe, obwohl jeder etwas anderes damit meint - eine
    /// Nummer und ein Häkchen „Ist Lösung" bedeuten bei der Schätzfrage nichts.
    /// </para>
    /// <para>
    /// <b>Die Schale kennt keinen Fragetyp.</b> Sie holt sich hier ein
    /// <see cref="ZeileneditorViewModel"/> und zeigt es in einem <c>ContentControl</c>; welches
    /// es ist, entscheidet <see cref="Zeileneditoren"/> anhand des Typs.
    /// </para>
    /// </summary>
    public partial class EditQuestionViewModel
    {
        private ZeileneditorViewModel? zeileneditor;

        /// <summary>
        /// Die Maske des Fragetyps - oder <c>null</c>, solange keine Frage geladen ist.
        /// </summary>
        public ZeileneditorViewModel? Zeileneditor
        {
            get => zeileneditor;
            private set
            {
                zeileneditor = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Baut die Maske zur geladenen Frage neu auf. Wird von <c>OnModelChanged</c> gerufen -
        /// eine Frage ohne Maske wäre ein leerer Editor.
        /// </summary>
        private void BaueZeileneditor()
        {
            if (Question == null)
            {
                Zeileneditor = null;
                return;
            }

            var composer = StepComposers.For(Question.Typ);

            Zeileneditor = Zeileneditoren.Fuer(composer, Question, Revalidate);
        }

        /// <summary>
        /// Schreibt zurück, was in der Maske steht - <b>vor</b> jedem Speichern.
        /// <para>
        /// <b>Sie schreibt nicht laufend mit.</b> Der Übersetzer räumt leere Zeilen weg und
        /// stempelt die Reihenfolge; liefe das bei jedem Tastendruck, verschwände eine Zeile
        /// unter dem Cursor, sobald man ihren Text löscht.
        /// </para>
        /// </summary>
        internal void UebernimmZeilen()
        {
            if (Question == null || Zeileneditor == null)
                return;

            Zeileneditor.SchreibZurueck(Question);
        }
    }
}
