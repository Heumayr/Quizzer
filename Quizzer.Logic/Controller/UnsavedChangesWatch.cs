using System.Diagnostics;

namespace Quizzer.Logic.Controller
{
    /// <summary>
    /// Meldet, wenn ein Controller mit ungespeicherten Aenderungen entsorgt wird.
    /// <para>
    /// Kein <c>UpsertAsync</c>, <c>UpdateAsync</c>, <c>InsertAsync</c> oder <c>DeleteAsync</c>
    /// schreibt von selbst - der Aufrufer muss <c>SaveChangesAsync</c> nachschieben. Vergisst er
    /// es, geht die Aenderung beim Entsorgen des Controllers verloren, <b>ohne jeden Fehler</b>.
    /// Genau das ist die teuerste Falle des Schreibpfads.
    /// </para>
    /// <para>
    /// Im laufenden Programm wird der Fall nur protokolliert - eine Ausnahme aus <c>Dispose</c>
    /// waere schlimmer als der Verlust. Die Testumgebung setzt hier einen Sammler ein und macht
    /// daraus einen roten Test.
    /// </para>
    /// </summary>
    public static class UnsavedChangesWatch
    {
        /// <summary>
        /// Was mit einer verworfenen Aenderung geschieht. Bekommt den Controllernamen und die
        /// Anzahl der betroffenen Eintraege.
        /// </summary>
        public static Action<string, int> Handler { get; set; } = LogToDebug;

        /// <summary>Setzt auf die reine Protokollierung zurueck.</summary>
        public static void ResetHandler() => Handler = LogToDebug;

        /// <summary>Meldet den Fall. Wirft nie - der Aufruf steht in einem <c>Dispose</c>.</summary>
        public static void Report(string controllerName, int changeCount)
        {
            if (changeCount <= 0)
                return;

            try
            {
                Handler(controllerName, changeCount);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UnsavedChangesWatch: der Melder selbst ist gescheitert: {ex.Message}");
            }
        }

        private static void LogToDebug(string controllerName, int changeCount)
        {
            Debug.WriteLine(
                $"{controllerName} wurde mit {changeCount} ungespeicherten Aenderungen entsorgt - "
                + "es fehlt ein SaveChangesAsync. Die Aenderungen sind verloren.");
        }
    }
}
