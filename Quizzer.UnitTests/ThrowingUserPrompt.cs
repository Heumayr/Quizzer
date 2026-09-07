using Quizzer.Base;

namespace Quizzer.UnitTests
{
    /// <summary>
    /// Der Rückfallwert für Rückfragen im Testlauf: er <b>wirft</b>, statt ein Fenster zu öffnen.
    /// <para>
    /// <b>Gemessen 2026-09-07.</b> Löste ein Test eine Meldung aus, ohne vorher
    /// <c>UserPrompt.Current</c> gegen <see cref="RecordingUserPrompt"/> zu tauschen, öffnete
    /// <c>MessageBoxUserPrompt</c> ein echtes modales Fenster im Testprozess. Der Lauf blieb
    /// stehen - bei Test 72 von rund 320, ohne rot zu werden und ohne einen Namen zu nennen. Er
    /// sah aus wie ein langsamer Lauf, kostete acht Minuten je Versuch und hinterließ als
    /// einzige Spur ein sichtbares Fenster mit dem Titel „Einstellungen".
    /// </para>
    /// <para>
    /// Der geworfene Text nennt Titel und Meldung - damit steht im Bericht, <i>welche</i>
    /// Rückfrage unerwartet kam, und nicht nur, dass eine kam.
    /// </para>
    /// </summary>
    public sealed class ThrowingUserPrompt : IUserPrompt
    {
        public bool Confirm(string message, string caption)
            => throw new InvalidOperationException(Text("Confirm", message, caption));

        public void Inform(string message, string caption = "")
            => throw new InvalidOperationException(Text("Inform", message, caption));

        private static string Text(string art, string message, string caption)
            => $"Unerwartete Rueckfrage ({art}) im Testlauf: [{caption}] {message}"
               + Environment.NewLine
               + "Der Test muss UserPrompt.Current gegen einen RecordingUserPrompt tauschen. "
               + "Ohne das oeffnete sich im Testprozess ein modales Fenster, und der Lauf blieb "
               + "stehen, statt rot zu werden.";
    }
}
