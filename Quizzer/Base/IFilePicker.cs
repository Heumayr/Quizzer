using Microsoft.Win32;
using System.IO;

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

        /// <summary>
        /// Welcher Ordner. <c>null</c> heißt: abgebrochen.
        /// </summary>
        /// <param name="start">Wo der Dialog aufgeht - ein nicht vorhandener Pfad wird übergangen.</param>
        string? AskForFolder(string titel, string start);
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

        public string? AskForFolder(string titel, string start)
        {
            var dialog = new OpenFolderDialog
            {
                Title = titel,
                Multiselect = false,
            };

            // Ein Startordner, den es nicht gibt, laesst den Dialog gar nicht erst aufgehen.
            if (!string.IsNullOrWhiteSpace(start) && Directory.Exists(start))
                dialog.InitialDirectory = start;

            return dialog.ShowDialog() == true ? dialog.FolderName : null;
        }
    }

    /// <summary>
    /// Zugang zur aktuellen Umsetzung. Tests tauschen <see cref="Current"/> gegen eine Fassung
    /// mit vorgegebenen Pfaden.
    /// </summary>
    public static class FilePicker
    {
        private static IFilePicker standard = new WindowsFilePicker();
        private static IFilePicker current = standard;

        public static IFilePicker Current
        {
            get => current;
            set => current = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Legt fest, worauf <see cref="Reset"/> zurueckfaellt. <b>Nur fuer Testlaeufe.</b>
        /// <para>
        /// <b>Dieselbe Falle wie bei <c>UserPrompt</c>, und sie hat am 2026-09-07 zweimal
        /// zugeschlagen.</b> Der Rueckfallwert war fest der echte Windows-Dialog. Eine
        /// Zusicherung, die einen Dateiwaehler ausloest, ohne <see cref="Current"/> zu tauschen,
        /// oeffnete damit ein echtes Dateifenster im Testprozess - der Lauf blieb stehen, ohne
        /// rot zu werden. Beim Meldungsfenster war wenigstens ein sichtbares Fenster da; hier
        /// ist nicht einmal das zu sehen.
        /// </para>
        /// </summary>
        internal static void UseAsDefaultForTests(IFilePicker? ersatz)
        {
            standard = ersatz ?? new WindowsFilePicker();
            current = standard;
        }

        /// <summary>Setzt auf die echten Dialoge zurueck.</summary>
        public static void Reset() => current = standard;

        public static string? AskForSaveTarget(string titel, string vorschlag, string filter)
            => Current.AskForSaveTarget(titel, vorschlag, filter);

        public static string? AskForExistingFile(string titel, string filter)
            => Current.AskForExistingFile(titel, filter);

        public static string? AskForFolder(string titel, string start)
            => Current.AskForFolder(titel, start);
    }
}
