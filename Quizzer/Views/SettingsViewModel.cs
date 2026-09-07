using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.Logic.Context;
using System.IO;
using System.Windows;
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
    /// <b>Nachgeschärft am selben Abend:</b> „pfad muss auswählbar sein ... datenbank
    /// einstellungen aufdröseln und einzeln eingeben ... connectionstring wird zusammengebaut".
    /// Deshalb der Ordnerdialog und die sechs Einzelfelder statt einer Zeichenfolge, die man
    /// auswendig können muss.
    /// </para>
    /// <para>
    /// <b>Die Maske ist auch vor dem Hauptfenster erreichbar.</b> Ist die Datenbank beim Start
    /// nicht da, führt die Meldung hierher - sonst säße jemand, der das Programm nur bekommen
    /// hat, bei einer falschen Verbindung fest und käme nirgends mehr hin. Genau darum gibt es
    /// auch die Verbindungsprobe: sonst hieße der Weg speichern, neu starten, scheitern.
    /// </para>
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private string datenordner = string.Empty;
        private string befund = string.Empty;

        private Datenbankangaben angaben = Datenbankangaben.Vorgabe();

        /// <summary>
        /// Die Verbindung, wie sie beim Oeffnen der Maske dastand - <b>schon durch den
        /// SqlConnectionStringBuilder gelaufen</b>.
        /// <para>
        /// Gegen <c>Settings.ConnectionString</c> verglichen meldete die Maske jeden Speichervorgang
        /// als Wechsel: der Bauer schreibt <c>Database=</c> zu <c>Initial Catalog=</c> um, und genau
        /// so steht es in der ausgelieferten <c>appsettings.json</c>. Gemessen 2026-09-07.
        /// </para>
        /// <para>
        /// <b>Beim Laden gesetzt, nicht in <c>Uebernimm</c></b> - „Zuruecksetzen" ruft dieselbe
        /// Stelle, und dort ist der Wechsel auf die Vorgaben ja gerade der Anlass fuer den Hinweis.
        /// </para>
        /// </summary>
        private string verbindungBeimLaden = string.Empty;

        /// <summary>
        /// Der Datenordner, wie er beim Öffnen der Maske dastand.
        /// <para>
        /// Auch er wirkt erst nach einem Neustart vollständig: <c>StaticResources</c> lädt
        /// Hintergründe, Zellbilder und Platzhalter <b>einmal beim Start</b> als Pinsel. Der neue
        /// Ordner gilt sofort für Medien, das Aussehen bleibt bis zum Neustart das alte.
        /// </para>
        /// </summary>
        private string datenordnerBeimLaden = string.Empty;

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

        /// <summary>Sagt, ob der eingetragene Ordner überhaupt existiert.</summary>
        public string DatenordnerHinweis =>
            string.IsNullOrWhiteSpace(Datenordner) ? "Kein Ordner eingetragen."
            : Directory.Exists(Datenordner) ? "Der Ordner ist vorhanden."
            : "Der Ordner existiert noch nicht - beim Speichern wird er angelegt.";

        /// <summary>Der Datenbankserver.</summary>
        public string Server
        {
            get => angaben.Server;
            set
            {
                angaben.Server = value;
                MeldeVerbindungGeaendert();
            }
        }

        /// <summary>Der Name der Datenbank auf diesem Server.</summary>
        public string Datenbank
        {
            get => angaben.Datenbank;
            set
            {
                angaben.Datenbank = value;
                MeldeVerbindungGeaendert();
            }
        }

        /// <summary>Anmeldung mit dem Windows-Konto statt mit Benutzer und Kennwort.</summary>
        public bool Windowsanmeldung
        {
            get => angaben.Windowsanmeldung;
            set
            {
                angaben.Windowsanmeldung = value;
                MeldeVerbindungGeaendert();
            }
        }

        /// <summary>Der Anmeldename - nur sichtbar, wenn nicht über Windows angemeldet wird.</summary>
        public string Benutzer
        {
            get => angaben.Benutzer;
            set
            {
                angaben.Benutzer = value;
                MeldeVerbindungGeaendert();
            }
        }

        /// <summary>
        /// Das Kennwort. Kommt aus einer <c>PasswordBox</c> über das Code-Behind der Maske -
        /// derselbe Weg wie bei der Anmeldung, und aus demselben Grund.
        /// </summary>
        public string Kennwort
        {
            get => angaben.Kennwort;
            set
            {
                angaben.Kennwort = value;
                MeldeVerbindungGeaendert();
            }
        }

        /// <summary>Dem Serverzertifikat vertrauen.</summary>
        public bool ZertifikatVertrauen
        {
            get => angaben.ZertifikatVertrauen;
            set
            {
                angaben.ZertifikatVertrauen = value;
                MeldeVerbindungGeaendert();
            }
        }

        /// <summary>Benutzer und Kennwort erscheinen nur, wenn sie gebraucht werden.</summary>
        public Visibility AnmeldedatenVisibility
            => Windowsanmeldung ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// Was aus den Feldern wird - mit unkenntlichem Kennwort. <b>Steht in der Maske</b>, damit
        /// sichtbar bleibt, was tatsächlich gespeichert wird.
        /// </summary>
        public string Verbindungsvorschau => angaben.VerbindungZumAnzeigen;

        /// <summary>
        /// Ein Hinweis, wenn die vorgefundene Verbindung nicht lesbar war und deshalb die
        /// Vorgaben in den Feldern stehen.
        /// </summary>
        public Visibility UnlesbarVisibility
            => angaben.Unlesbar ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Was die Verbindungsprobe zuletzt ergeben hat.</summary>
        public string Verbindungsbefund
        {
            get => befund;
            private set
            {
                befund = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BefundVisibility));
            }
        }

        /// <summary>Der Befund erscheint erst, wenn geprüft wurde.</summary>
        public Visibility BefundVisibility
            => string.IsNullOrEmpty(Verbindungsbefund) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>Wo die eigenen Werte abgelegt werden. Steht in der Maske, damit man sie findet.</summary>
        public string Ablageort => UserSettings.FilePath;

        protected override Task OnloadAsync()
        {
            Datenordner = Settings.FilePathQuizzer;

            Uebernimm(Datenbankangaben.Zerlege(Settings.ConnectionString));

            verbindungBeimLaden = angaben.Verbindung;
            datenordnerBeimLaden = Datenordner.Trim();

            return Task.CompletedTask;
        }

        public override Task VMSaveAsync() => Task.CompletedTask;

        private void Uebernimm(Datenbankangaben neue)
        {
            angaben = neue;

            Verbindungsbefund = string.Empty;

            MeldeVerbindungGeaendert();
            OnPropertyChanged(nameof(UnlesbarVisibility));
        }

        /// <summary>
        /// Alle abgeleiteten Angaben an einer Stelle melden.
        /// <para>
        /// <b>Absichtlich gebündelt:</b> jedes einzelne Feld verändert die zusammengebaute
        /// Zeichenfolge, und eine vergessene Meldung fiele nur dadurch auf, dass die Vorschau
        /// stehenbleibt - also gar nicht.
        /// </para>
        /// </summary>
        private void MeldeVerbindungGeaendert()
        {
            OnPropertyChanged(nameof(Server));
            OnPropertyChanged(nameof(Datenbank));
            OnPropertyChanged(nameof(Windowsanmeldung));
            OnPropertyChanged(nameof(Benutzer));
            OnPropertyChanged(nameof(Kennwort));
            OnPropertyChanged(nameof(ZertifikatVertrauen));
            OnPropertyChanged(nameof(AnmeldedatenVisibility));
            OnPropertyChanged(nameof(Verbindungsvorschau));
        }

        private RelayCommand? chooseFolderCommand;

        /// <summary>Den Datenordner aussuchen statt ihn abzutippen.</summary>
        public ICommand ChooseFolderCommand => chooseFolderCommand ??= new RelayCommand(_ =>
        {
            var gewaehlt = FilePicker.AskForFolder("Datenordner wählen", Datenordner);

            if (gewaehlt != null)
                Datenordner = gewaehlt;
        });

        private AsyncRelayCommand? testConnectionCommand;

        /// <summary>Die Verbindung ausprobieren, ohne die Maske zu verlassen.</summary>
        public ICommand TestConnectionCommand => testConnectionCommand ??= new AsyncRelayCommand(
            async _ =>
            {
                // AsyncRelayCommand sperrt sich waehrend des Laufs selbst - ein zweiter Klick
                // kann hier also nicht dazwischenkommen.
                Verbindungsbefund = "Wird geprüft …";

                Verbindungsbefund = await DatabaseInitializer.TesteVerbindungAsync(
                    angaben.Verbindung);
            });

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

            // Ohne Server oder Datenbanknamen wird nicht gespeichert. Bei leerem Namen
            // entsteht "Initial Catalog=" - und das Programm legt sein ganzes Schema in der
            // Systemdatenbank master an, ohne beim Start irgendetwas zu fragen. Der Pruefknopf
            // sagt es schon laenger; das Speichern tat es nicht.
            if (string.IsNullOrWhiteSpace(angaben.Server) || string.IsNullOrWhiteSpace(angaben.Datenbank))
            {
                UserPrompt.Inform(
                    "Server und Datenbank müssen ausgefüllt sein."
                    + Environment.NewLine + Environment.NewLine
                    + "Ohne Datenbanknamen legt das Programm seine Tabellen in der "
                    + "Systemdatenbank an.",
                    "Einstellungen");

                return;
            }

            var hinweis = Neustarthinweis();

            UserSettings.Save(Datenordner, angaben.Verbindung);

            if (hinweis != null)
                UserPrompt.Inform(hinweis, "Einstellungen");

            Saved = true;
            Window?.Close();
        });

        /// <summary>
        /// Was der Spielleiter nach dem Speichern wissen muss - oder <c>null</c>, wenn sich
        /// nichts geändert hat, das einen Neustart braucht.
        /// <para>
        /// <b>Beides wirkt halb sofort und halb gar nicht.</b> Die neue Verbindung benutzt jeder
        /// <c>DataContext</c> ab dem nächsten Zugriff, geprüft und migriert wird sie aber nur
        /// beim Programmstart - ohne Hinweis käme beim nächsten Klick auf „Spiele" eine rohe
        /// <c>SqlException</c> oder „Invalid column name". Der neue Datenordner gilt sofort für
        /// Medien, während Hintergründe und Zellbilder aus dem alten weiterleuchten:
        /// <c>StaticResources</c> lädt sie einmal beim Start.
        /// </para>
        /// <para>
        /// <b>Es wird nur gemeldet, was sich wirklich geändert hat.</b> Ein Hinweis bei jedem
        /// Speichern wird weggeklickt und damit auch dann nicht gelesen, wenn er zählt.
        /// </para>
        /// </summary>
        private string? Neustarthinweis()
        {
            var teile = new List<string>();

            if (!string.Equals(datenordnerBeimLaden, Datenordner.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                teile.Add("Bilder und Design werden beim Start geladen - bis zum Neustart "
                        + "zeigt das Programm die aus dem bisherigen Ordner.");
            }

            if (!string.Equals(verbindungBeimLaden, angaben.Verbindung, StringComparison.Ordinal))
            {
                teile.Add("Die Datenbank wird erst beim Start geprüft und, wenn nötig, angelegt "
                        + "oder auf den neuesten Stand gebracht.");
            }

            if (teile.Count == 0)
                return null;

            return "Bitte das Programm neu starten."
                + Environment.NewLine + Environment.NewLine
                + string.Join(Environment.NewLine + Environment.NewLine, teile);
        }

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

            Uebernimm(Datenbankangaben.Zerlege(Settings.ConnectionString));
        });
    }
}
