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
        private static IUserPrompt current = new MessageBoxUserPrompt();

        public static IUserPrompt Current
        {
            get => current;
            set => current = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Setzt auf die echten Meldungsfenster zurueck.</summary>
        public static void Reset() => current = new MessageBoxUserPrompt();

        public static bool Confirm(string message, string caption) => Current.Confirm(message, caption);

        public static void Inform(string message, string caption = "") => Current.Inform(message, caption);
    }
}
