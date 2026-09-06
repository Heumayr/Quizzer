using Quizzer.Base;
using Quizzer.DataModels;
using System.IO;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>
    /// Die Einstellungen, die der Nutzer selbst setzt: wo die Medien liegen und welche Datenbank
    /// benutzt wird.
    /// <para>
    /// <b>Nutzerentscheidung vom 2026-09-06:</b> „wo das programm die ressourcen ablegt muss auch
    /// im programm konfigurierbar sein ... eigentlich alle einstellungen" - mit der Begründung
    /// „damit ich das programm weitergeben kann ohne großen aufwand für den endnutzer".
    /// </para>
    /// <para>
    /// <b>Die Maske ist auch vor dem Hauptfenster erreichbar.</b> Ist die Datenbank beim Start
    /// nicht da, führt die Meldung hierher - sonst säße jemand, der das Programm nur bekommen
    /// hat, bei einer falschen Verbindung fest und käme nirgends mehr hin.
    /// </para>
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private string datenordner = string.Empty;
        private string verbindung = string.Empty;

        /// <summary>Ob gespeichert wurde. Der Aufrufer liest das nach dem Schließen.</summary>
        public bool Saved { get; private set; }

        /// <summary>Der Ordner mit Bildern, Medien und Designs.</summary>
        public string Datenordner
        {
            get => datenordner;
            set
            {
                datenordner = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DatenordnerHinweis));
            }
        }

        /// <summary>Die Verbindungszeichenfolge zur Spieldatenbank.</summary>
        public string Verbindung
        {
            get => verbindung;
            set
            {
                verbindung = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Sagt, ob der eingetragene Ordner überhaupt existiert.</summary>
        public string DatenordnerHinweis =>
            string.IsNullOrWhiteSpace(Datenordner) ? "Kein Ordner eingetragen."
            : Directory.Exists(Datenordner) ? "Der Ordner ist vorhanden."
            : "Der Ordner existiert noch nicht - beim Speichern wird er angelegt.";

        /// <summary>Wo die eigenen Werte abgelegt werden. Steht in der Maske, damit man sie findet.</summary>
        public string Ablageort => UserSettings.FilePath;

        protected override Task OnloadAsync()
        {
            Datenordner = Settings.FilePathQuizzer;
            Verbindung = Settings.ConnectionString;

            return Task.CompletedTask;
        }

        public override Task VMSaveAsync() => Task.CompletedTask;

        private RelayCommand? saveCommand;

        public ICommand SaveCommand => saveCommand ??= new RelayCommand(_ =>
        {
            if (!string.IsNullOrWhiteSpace(Datenordner))
            {
                try
                {
                    Directory.CreateDirectory(Datenordner);
                }
                catch (Exception ex)
                {
                    UserPrompt.Inform(
                        "Der Ordner ließ sich nicht anlegen:" + Environment.NewLine + ex.Message,
                        "Einstellungen");

                    return;
                }
            }

            UserSettings.Save(Datenordner, Verbindung);

            Saved = true;
            Window?.Close();
        });

        private RelayCommand? cancelCommand;

        public ICommand CancelCommand => cancelCommand ??= new RelayCommand(_ =>
        {
            Saved = false;
            Window?.Close();
        });

        private RelayCommand? resetCommand;

        /// <summary>
        /// Zurück auf die ausgelieferten Vorgaben. Die eigene Datei wird gelöscht, nicht geleert -
        /// eine Datei mit leeren Feldern sähe aus wie eine gesetzte Einstellung.
        /// </summary>
        public ICommand ResetCommand => resetCommand ??= new RelayCommand(_ =>
        {
            if (!UserPrompt.Confirm(
                "Die eigenen Einstellungen werden gelöscht und die ausgelieferten Vorgaben "
                + "gelten wieder." + Environment.NewLine + Environment.NewLine
                + "Fortfahren?",
                "Einstellungen zurücksetzen"))
                return;

            try
            {
                if (File.Exists(UserSettings.FilePath))
                    File.Delete(UserSettings.FilePath);
            }
            catch (Exception ex)
            {
                UserPrompt.Inform(
                    "Die Datei ließ sich nicht löschen:" + Environment.NewLine + ex.Message,
                    "Einstellungen");

                return;
            }

            Settings.LoadSettings();

            Datenordner = Settings.FilePathQuizzer;
            Verbindung = Settings.ConnectionString;
        });
    }
}
