using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;

namespace Quizzer.DataModels.Questions.Schrittbau
{
    /// <summary>
    /// Übersetzt zwischen der Sprache eines Fragetyps - Antwortzeile, Hinweis, Auflösung - und
    /// den <c>QuestionStepResource</c>-Schritten, die das Spiel kennt.
    /// <para>
    /// <b>Warum überhaupt eine eigene Schicht:</b> das Schrittmodell ist für alle fünf Typen
    /// dasselbe, obwohl jeder etwas anderes damit meint. Der Editor zeigt deshalb heute jedem Typ
    /// dieselbe Tabelle samt Nummer und Häkchen, und für die Hälfte der Typen bedeutet die Hälfte
    /// der Spalten nichts.
    /// </para>
    /// <para>
    /// <b>Diese Schicht ist WPF-frei</b>, damit jede Regel ohne Oberflächen-Thread prüfbar bleibt
    /// - dieselbe Begründung, aus der <see cref="QuestionValidator"/> hier liegt und nicht in den
    /// ViewModels.
    /// </para>
    /// </summary>
    public interface IStepComposer
    {
        /// <summary>Für welchen Fragetyp.</summary>
        QuestionType Typ { get; }

        /// <summary>Die Überschrift über der Zeilenliste - leer, wenn der Typ keine hat.</summary>
        string ZeilenTitel { get; }

        /// <summary>Die Überschrift über dem Abschlussfeld - leer, wenn der Typ keines hat.</summary>
        string AbschlussTitel { get; }

        /// <summary>
        /// Liest die Schritte einer Frage in ein <see cref="Schrittbild"/>.
        /// <para>
        /// <b>Die Schritte werden durchgereicht, nicht kopiert.</b> Was der Typ nicht modelliert,
        /// landet in <see cref="Schrittbild.Mitgefuehrt"/> und fährt unverändert mit.
        /// </para>
        /// </summary>
        Schrittbild Lies(QuestionBase frage);

        /// <summary>
        /// Schreibt ein Schrittbild in die Frage zurück und setzt dabei, was der Typ ableitet
        /// (etwa die Zahl der wählbaren Antworten bei Multiple Choice).
        /// </summary>
        void Schreib(QuestionBase frage, Schrittbild bild);
    }
}
