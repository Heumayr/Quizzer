using Microsoft.Win32;

namespace Quizzer.Base
{
    /// <summary>
    /// Die Auswahl einer Datei. Gekapselt aus demselben Grund wie <see cref="IUserPrompt"/>: ein
    /// Dateidialog bliebe im Testlauf stehen.
    /// </summary>
    public interface IFilePicker
    {
        /// <summary>Wohin gespeichert wird. <c>null</c> heißt: abgebrochen.</summary>
        string? AskForSaveTarget(string titel, string vorschlag, string filter);

        /// <summary>Was geöffnet wird. <c>null</c> heißt: abgebrochen.</summary>
        string? AskForExistingFile(string titel, string filter);
    }

    /// <summary>Der Regelfall im laufenden Programm: die Dialoge von Windows.</summary>
    public sealed class WindowsFilePicker : IFilePicker
    {
        public string? AskForSaveTarget(string titel, string vorschlag, string filter)
        {
            var dialog = new SaveFileDialog
            {
                Title = titel,
                FileName = vorschlag,
                Filter = filter,
                AddExtension = true,
                OverwritePrompt = true,
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? AskForExistingFile(string titel, string filter)
        {
            var dialog = new OpenFileDialog
            {
                Title = titel,
                Filter = filter,
                CheckFileExists = true,
                Multiselect = false,
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }

    /// <summary>
    /// Zugang zur aktuellen Umsetzung. Tests tauschen <see cref="Current"/> gegen eine Fassung
    /// mit vorgegebenen Pfaden.
    /// </summary>
    public static class FilePicker
    {
        private static IFilePicker current = new WindowsFilePicker();

        public static IFilePicker Current
        {
            get => current;
            set => current = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Setzt auf die echten Dialoge zurueck.</summary>
        public static void Reset() => current = new WindowsFilePicker();

        public static string? AskForSaveTarget(string titel, string vorschlag, string filter)
            => Current.AskForSaveTarget(titel, vorschlag, filter);

        public static string? AskForExistingFile(string titel, string filter)
            => Current.AskForExistingFile(titel, filter);
    }
}
