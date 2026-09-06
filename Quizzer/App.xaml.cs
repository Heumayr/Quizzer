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

            CatchWhatWouldEndTheEvening();

            Settings.LoadSettings();

            if (!Anmelden())
            {
                Shutdown();
                return;
            }

            var mainVm = new MainViewModel();
            var window = new MainWindow { DataContext = mainVm };
            window.Show();
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
