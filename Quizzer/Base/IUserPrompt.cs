using System.Windows;

namespace Quizzer.Base
{
    /// <summary>
    /// Rueckfragen an den Spielleiter. Kapselt <see cref="MessageBox"/>, damit der Spielablauf
    /// auch ohne Bedienung durchlaufen kann - ein modales Fenster bliebe im Test stehen.
    /// </summary>
    public interface IUserPrompt
    {
        /// <summary>Ja/Nein-Rueckfrage. <c>true</c> bedeutet Ja.</summary>
        bool Confirm(string message, string caption);

        /// <summary>Hinweis, den der Spielleiter nur zur Kenntnis nimmt.</summary>
        void Inform(string message, string caption = "");
    }

    /// <summary>Der Regelfall im laufenden Programm: echte Meldungsfenster.</summary>
    public sealed class MessageBoxUserPrompt : IUserPrompt
    {
        public bool Confirm(string message, string caption)
            => MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question)
               == MessageBoxResult.Yes;

        public void Inform(string message, string caption = "")
            => MessageBox.Show(message, caption);
    }

    /// <summary>
    /// Zugang zur aktuellen Rueckfrage-Umsetzung. Tests tauschen <see cref="Current"/> gegen eine
    /// Fassung mit vorgegebenen Antworten und pruefen hinterher, was gefragt wurde.
    /// </summary>
    public static class UserPrompt
    {
        private static IUserPrompt standard = new MessageBoxUserPrompt();
        private static IUserPrompt current = standard;

        public static IUserPrompt Current
        {
            get => current;
            set => current = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Legt fest, worauf <see cref="Reset"/> zurueckfaellt. <b>Nur fuer Testlaeufe.</b>
        /// <para>
        /// <b>Gemessen am 2026-09-07, und es hat eine Stunde gekostet.</b> Der Rueckfallwert war
        /// fest das echte Meldungsfenster. Ein Test, der eine Meldung ausloest, ohne vorher
        /// <see cref="Current"/> zu tauschen, oeffnete damit ein <b>modales Fenster im
        /// Testprozess</b> - der Lauf blieb bei Test 72 von rund 320 stehen, ohne rot zu werden,
        /// ohne einen Namen zu nennen und ohne dass etwas im Bericht stand. Sichtbar war es erst,
        /// als die Fenster des Prozesses aufgezaehlt wurden: eines mit dem Titel
        /// „Einstellungen".
        /// </para>
        /// <para>
        /// <b>Ein Aufhaenger ist schlimmer als ein roter Test</b>, weil er keinen Befund
        /// hinterlaesst. Deshalb setzt der Testlauf hier eine Fassung ein, die wirft.
        /// </para>
        /// </summary>
        internal static void UseAsDefaultForTests(IUserPrompt? ersatz)
        {
            standard = ersatz ?? new MessageBoxUserPrompt();
            current = standard;
        }

        /// <summary>Setzt auf die echten Meldungsfenster zurueck.</summary>
        public static void Reset() => current = standard;

        public static bool Confirm(string message, string caption) => Current.Confirm(message, caption);

        public static void Inform(string message, string caption = "") => Current.Inform(message, caption);
    }
}
