using System.Diagnostics;
using System.Windows;

namespace Quizzer.Base
{
    /// <summary>
    /// Startet das Programm neu - nach dem Abmelden und nach „Daten von diesem Rechner entfernen"
    /// (M28). Ein Neustart statt eines Zurückdrehens von Hand: im Servermodus zeigen
    /// Datenbank, Sitzung und die beim ersten Zugriff eingefrorenen Pinsel auf das Konto, und jede
    /// vergessene Stelle arbeitete danach mit dem alten weiter.
    /// <para>
    /// Ein ViewModel ruft <see cref="Restart"/> statt selbst einen Prozess zu starten; Tests tauschen
    /// <see cref="Current"/>.
    /// </para>
    /// </summary>
    public static class AppRestart
    {
        private static Action standard = RestartProcess;
        private static Action current = standard;

        public static Action Current
        {
            get => current;
            set => current = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Legt fest, worauf <see cref="Reset"/> zurückfällt. <b>Nur für Testläufe</b> - der echte
        /// Neustart startete dort den Testprozess ein zweites Mal (vgl. <see cref="UserPrompt"/>).
        /// </summary>
        internal static void UseAsDefaultForTests(Action? ersatz)
        {
            standard = ersatz ?? RestartProcess;
            current = standard;
        }

        public static void Reset() => current = standard;

        public static void Restart() => current();

        private static void RestartProcess()
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = false,
                WorkingDirectory = AppContext.BaseDirectory,
            });

            Application.Current.Shutdown();
        }
    }
}
