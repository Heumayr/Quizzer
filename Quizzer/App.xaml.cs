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

            var mainVm = new MainViewModel();
            var window = new MainWindow { DataContext = mainVm };
            window.Show();
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
