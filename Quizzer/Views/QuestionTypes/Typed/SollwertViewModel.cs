using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions.Schrittbau;

namespace Quizzer.Views.QuestionTypes.Typed
{
    /// <summary>
    /// Schätzfrage: <b>keine Zeilenliste</b>, nur das, was am Ende steht.
    /// <para>
    /// Der Typ hat keine Antwortmöglichkeiten und keine Hinweise - das Spiel ermittelt aus
    /// Sollwert und Einheit selbst, wer am nächsten liegt. Bisher bekam er trotzdem ein volles
    /// Schritt-Raster samt Spalte „Ist Lösung", die für ihn nie etwas bedeuten kann.
    /// </para>
    /// </summary>
    public sealed class SollwertViewModel : ZeileneditorViewModel
    {
        internal SollwertViewModel(IStepComposer composer, QuestionBase frage, Action melde)
            : base(composer, frage, melde)
        {
        }

        public override bool DarfSortieren => false;

        public override bool DarfZeilenAendern => false;
    }
}
