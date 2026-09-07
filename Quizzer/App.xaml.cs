using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using Quizzer.DataModels;
using Quizzer.Views;
using Quizzer.Views.StaticRessources;

namespace Quizzer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            FensterModusFuerAnmeldungSetzen();

            CatchWhatWouldEndTheEvening();

            Settings.LoadSettings();

            // Die Werte des Nutzers ueber die ausgelieferten Vorgaben. Ausdruecklich hier und
            // nicht in LoadSettings: DataContext laedt die Einstellungen ebenfalls, und ein
            // Testlauf, der dabei die Werte des Nutzers erwischt, schreibt in dessen echte
            // Spieldatenbank.
            UserSettings.Apply();

            if (!DatenbankMitZweitemVersuch())
            {
                Shutdown();
                return;
            }

            DatenordnerPruefen();

            if (!Anmelden())
            {
                Shutdown();
                return;
            }

            var mainVm = new MainViewModel();
            var window = new MainWindow { DataContext = mainVm };

            // Ab hier beendet das Schliessen des Hauptfensters das Programm - vorher durfte es
            // das ausdruecklich nicht (siehe FensterModusFuerAnmeldungSetzen).
            window.Closed += (_, _) => Shutdown();

            MainWindow = window;
            window.Show();
        }

        /// <summary>
        /// Sorgt dafuer, dass das Schliessen des Anmeldefensters das Programm nicht beendet.
        /// <para>
        /// <b>Gemeldet 2026-09-06:</b> „sobald ich anmelden klicke ... beendet das programm".
        /// Die Ursache ist der Standard von WPF: <c>ShutdownMode</c> ist
        /// <c>OnLastWindowClose</c>, und beim Start ist das Anmeldefenster das <b>einzige</b>
        /// Fenster. Sobald es sich schliesst, sind null Fenster offen - WPF beendet die
        /// Anwendung, und das <c>Show()</c> des Hauptfensters kommt nie zum Zug.
        /// </para>
        /// <para>
        /// <b>Kein Test konnte das je finden.</b> <c>UiTestHost</c> erzeugt seine
        /// <c>Application</c> mit <c>ShutdownMode = OnExplicitShutdown</c> - also genau mit der
        /// Einstellung, deren Fehlen der Defekt ist. Der Pruefstand hat den Fehler zugedeckt.
        /// </para>
        /// </summary>
        internal static void FensterModusFuerAnmeldungSetzen()
            => Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        /// <summary>
        /// Zieht die Datenbank auf den Stand der Migrationen, bevor irgendetwas sie liest.
        /// <para>
        /// <b>Nutzerentscheidung vom 2026-09-06 (Frage F03).</b> <c>EnsureMigrated</c> gab es
        /// vorher schon, aber niemand rief es - und das traf den Rueckweg: eine zurueckgespielte
        /// Sicherung liegt naturgemaess vor der juengsten Migration, und ohne diesen Aufruf
        /// haette der Spielleiter danach <c>dotnet ef database update</c> von der Kommandozeile
        /// gebraucht. Das ist der Rueckweg eines Entwicklers, nicht seiner.
        /// </para>
        /// <para>
        /// Der Aufruf steht <b>vor</b> der Anmeldung, denn die liest bereits Mitspieler.
        /// Scheitert er, wird gemeldet und beendet: mit halbem Schema weiterzulaufen ergibt nur
        /// unverstaendliche Fehler an spaeterer Stelle.
        /// </para>
        /// </summary>
        internal static bool DatenbankAufStandBringen()
        {
            try
            {
                if (!Logic.Context.DatabaseInitializer.Exists())
                    return NeueDatenbankAnlegen();

                Logic.Context.DatabaseInitializer.EnsureMigrated();
                return true;
            }
            catch (Exception ex)
            {
                // Ueber UserPrompt, nicht ueber MessageBox: ein modales Fenster bliebe im Test
                // stehen (Projektregel, siehe ViewModelIndependenceUnitTests).
                Base.UserPrompt.Inform(
                    "Die Datenbank ist nicht erreichbar."
                    + Environment.NewLine + Environment.NewLine
                    + "Ziel: " + Logic.Context.DatabaseInitializer.DescribeTarget()
                    + Environment.NewLine + Environment.NewLine
                    + ex.Message
                    + Environment.NewLine + Environment.NewLine
                    + "Meist fehlt SQL Server LocalDB. Die Verbindung lässt sich in den "
                    + "Einstellungen ändern.",
                    "Quizzer");

                return false;
            }
        }

        /// <summary>
        /// Der Startschritt mit einem zweiten Versuch: scheitert er, fuehrt der Weg in die
        /// Einstellungen und danach noch einmal hierher.
        /// <para>
        /// <b>Ohne das saesse fest, wer das Programm nur bekommen hat.</b> Die Einstellungen
        /// haengen sonst am Hauptfenster, und das gibt es an dieser Stelle noch nicht - eine
        /// falsche Verbindungszeichenfolge waere damit eine Sackgasse.
        /// </para>
        /// </summary>
        private static bool DatenbankMitZweitemVersuch()
        {
            if (DatenbankAufStandBringen())
                return true;

            if (!Base.UserPrompt.Confirm(
                    "Sollen die Einstellungen jetzt geöffnet werden?",
                    "Quizzer einrichten"))
                return false;

            new Views.SettingsView().ShowDialog();

            return DatenbankAufStandBringen();
        }

        /// <summary>
        /// Fragt, ob eine lokale Datenbank angelegt werden soll - und legt sie an.
        /// <para>
        /// <b>Nutzerentscheidung vom 2026-09-06:</b> „das programm soll wenn keine db da ist
        /// nachfragen ob eine lokale angelegt werden soll ... damit ich das programm weitergeben
        /// kann ohne großen aufwand für den endnutzer".
        /// </para>
        /// <para>
        /// <b>Gemessen am selben Tag:</b> vorher entstand sie <b>stillschweigend</b> -
        /// <c>EnsureMigrated</c> legt eine fehlende Datenbank an und spielt alle Migrationen ein,
        /// ohne ein Wort. Für den, der das Programm gerade zum ersten Mal startet, geschieht
        /// damit unsichtbar etwas auf seinem Rechner.
        /// </para>
        /// </summary>
        private static bool NeueDatenbankAnlegen()
        {
            var ziel = Logic.Context.DatabaseInitializer.DescribeTarget();

            var ja = Base.UserPrompt.Confirm(
                "Es wurde noch keine Datenbank gefunden."
                + Environment.NewLine + Environment.NewLine
                + "Soll jetzt eine lokale Datenbank angelegt werden?"
                + Environment.NewLine + Environment.NewLine
                + "Ziel: " + ziel,
                "Quizzer einrichten");

            if (!ja)
                return false;

            Logic.Context.DatabaseInitializer.EnsureMigrated();

            Base.UserPrompt.Inform(
                "Die Datenbank wurde angelegt:" + Environment.NewLine + ziel,
                "Quizzer einrichten");

            return true;
        }

        /// <summary>
        /// Sagt es, wenn der Datenordner fehlt - und führt in die Einstellungen.
        /// <para>
        /// <b>Gemessen 2026-09-07.</b> Für die Datenbank gab es diesen Weg längst, für den
        /// Datenordner nicht. Fehlt er, wirft nichts: <c>StaticResources</c> fällt für jedes
        /// nicht gefundene Bild auf <b>Schwarz</b> zurück. Wer das Programm gerade bekommen hat,
        /// startet es also, sieht eine schwarze Oberfläche und bekommt <b>kein einziges Wort</b>
        /// dazu - obwohl nur ein Pfad falsch steht.
        /// </para>
        /// <para>
        /// <b>Es wird nicht beendet.</b> Anders als bei der Datenbank ist ein fehlender
        /// Datenordner nicht tödlich - das Programm läuft, es sieht nur falsch aus. Wer die
        /// Rückfrage verneint, spielt weiter.
        /// </para>
        /// <para>
        /// <b>Und der Ordner wird nicht heimlich angelegt.</b> Ein leerer Ordner behebt nichts:
        /// die Bilder sind dann immer noch weg, und der Hinweis käme beim nächsten Start nicht
        /// mehr. Angelegt wird er beim Speichern in den Einstellungen, wo es der Nutzer sieht.
        /// </para>
        /// </summary>
        internal static void DatenordnerPruefen()
        {
            var ordner = Settings.FilePathQuizzer;

            if (!string.IsNullOrWhiteSpace(ordner) && System.IO.Directory.Exists(ordner))
                return;

            var oeffnen = Base.UserPrompt.Confirm(
                "Der Datenordner wurde nicht gefunden."
                + Environment.NewLine + Environment.NewLine
                + "Ordner: " + (string.IsNullOrWhiteSpace(ordner) ? "(nicht eingetragen)" : ordner)
                + Environment.NewLine + Environment.NewLine
                + "Ohne ihn bleiben Hintergründe, Zellbilder und Spielerbilder schwarz, und "
                + "Medien lassen sich nicht ablegen. Gespielt werden kann trotzdem."
                + Environment.NewLine + Environment.NewLine
                + "Sollen die Einstellungen jetzt geöffnet werden?",
                "Quizzer einrichten");

            if (!oeffnen)
                return;

            new Views.SettingsView().ShowDialog();
        }

        /// <summary>
        /// Fragt am Anfang, wer das Quiz leitet.
        /// <para>
        /// Die Wahl entscheidet, welche Fragen zur Verfuegung stehen. Sie steht deshalb vor dem
        /// Hauptfenster und nicht darin.
        /// </para>
        /// <para>
        /// <b>Solange niemand als Spielleiter angelegt ist, geht es ohne Anmeldung weiter.</b>
        /// Sonst waere das Programm nach dem Einspielen dieser Aenderung nicht mehr zu oeffnen -
        /// und der Haken laesst sich nur darin setzen.
        /// </para>
        /// </summary>
        private static bool Anmelden()
        {
            var fenster = new Views.LoginView();

            if (fenster.DataContext is Views.LoginViewModel vm)
            {
                fenster.ShowDialog();

                if (vm.SignedIn)
                    return true;

                // Kein Spielleiter angelegt: weitermachen, sonst sperrt sich der Nutzer aus.
                return !vm.HasModerators;
            }

            return true;
        }

        /// <summary>
        /// Faengt Fehler ab, die sonst die Anwendung beenden.
        /// <para>
        /// Das Programm ist voller <c>async void</c>-Ereignisbehandlungen - jedes
        /// <c>Loaded</c>, <c>Closed</c> und jeder Rueckruf aus dem Buzzer-Hub. Wirft dort etwas,
        /// endet die Anwendung ohne Rueckfrage. Mitten in einem Spielabend heisst das: Spielfeld
        /// weg, Beamerbild weg, alle Telefone getrennt.
        /// </para>
        /// <para>
        /// Ein Fehlerfenster ist dann das kleinere Uebel. Der Spielleiter kann weiterspielen oder
        /// bewusst beenden; verloren ist hoechstens der eine Vorgang.
        /// </para>
        /// </summary>
        private void CatchWhatWouldEndTheEvening()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // Ein Task, dessen Fehler niemand abholt. Ohne das Zeichnen als "behandelt" beendet
            // der Aufraeumer den Prozess.
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                args.SetObserved();
                ExceptionManager.HandleException(args.Exception);
            };
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Als behandelt zeichnen, bevor irgendetwas anderes geschieht - sonst faehrt die
            // Anwendung herunter, waehrend das Fehlerfenster noch aufgeht.
            e.Handled = true;

            ExceptionManager.HandleException(e.Exception);
        }
    }
}
