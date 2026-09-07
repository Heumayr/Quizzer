using Quizzer.Base;

namespace Quizzer.UnitTests
{
    /// <summary>
    /// Der Rückfallwert für Dateidialoge im Testlauf: er <b>wirft</b>, statt ein Fenster zu
    /// öffnen.
    /// <para>
    /// <b>Dieselbe Falle wie bei <see cref="ThrowingUserPrompt"/>, und die teurere von beiden.</b>
    /// Löste ein Test einen Dateiwähler aus, ohne <c>FilePicker.Current</c> zu tauschen, öffnete
    /// <c>WindowsFilePicker</c> einen echten Dialog im Testprozess. Der Lauf blieb stehen — und
    /// anders als beim Meldungsfenster ist dabei <b>nicht einmal ein sichtbares Fenster</b> zu
    /// sehen, an dem man es erkennen könnte. Gemessen 2026-09-07, zweimal, je 400 Sekunden.
    /// </para>
    /// </summary>
    public sealed class ThrowingFilePicker : IFilePicker
    {
        public string? AskForSaveTarget(string titel, string vorschlag, string filter)
            => throw Fehler("AskForSaveTarget", titel);

        public string? AskForExistingFile(string titel, string filter)
            => throw Fehler("AskForExistingFile", titel);

        public string? AskForFolder(string titel, string start)
            => throw Fehler("AskForFolder", titel);

        private static InvalidOperationException Fehler(string art, string titel)
            => new($"Unerwarteter Dateidialog im Testlauf: {art} [{titel}]"
                   + Environment.NewLine
                   + "Der Test muss FilePicker.Current tauschen. Ohne das oeffnete sich im "
                   + "Testprozess ein echter Dialog, und der Lauf blieb stehen, statt rot zu "
                   + "werden - ohne sichtbares Fenster.");
    }
}
