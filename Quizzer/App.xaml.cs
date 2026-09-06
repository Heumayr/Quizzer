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

            if (!DatenbankAufStandBringen())
            {
                Shutdown();
                return;
            }

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
                Logic.Context.DatabaseInitializer.EnsureMigrated();
                return true;
            }
            catch (Exception ex)
            {
                // Ueber UserPrompt, nicht ueber MessageBox: ein modales Fenster bliebe im Test
                // stehen (Projektregel, siehe ViewModelIndependenceUnitTests).
                Base.UserPrompt.Inform(
                    "Die Datenbank konnte nicht auf den aktuellen Stand gebracht werden."
                    + Environment.NewLine + Environment.NewLine
                    + ex.Message
                    + Environment.NewLine + Environment.NewLine
                    + "Prüfen Sie die Verbindungszeichenfolge in der appsettings.json.",
                    "Quizzer");

                return false;
            }
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
