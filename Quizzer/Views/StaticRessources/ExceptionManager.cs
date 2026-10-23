using Quizzer.Views.HelperViewModels;
using System.Windows;

namespace Quizzer.Views.StaticRessources
{
    public static class ExceptionManager
    {
        /// <summary>
        /// Was mit einer gefangenen Ausnahme geschieht. Im laufenden Programm ist das das
        /// Fehlerfenster; Tests setzen hier einen Sammler ein, sonst wird jeder Fehler in ein
        /// Fenster geschluckt und der Test bleibt gruen.
        /// </summary>
        public static Action<Exception> Handler { get; set; } = ShowExceptionWindow;

        /// <summary>Setzt <see cref="Handler"/> auf das Fehlerfenster zurueck.</summary>
        public static void ResetHandler() => Handler = ShowExceptionWindow;

        /// <summary>
        /// Nimmt eine gefangene Ausnahme entgegen - von jedem Thread aus.
        /// <para>
        /// Die Behandlung laeuft auf dem Oberflaechen-Thread, weil sie im laufenden Programm ein
        /// Fenster baut. Die Buzzer-Rueckrufe kommen aus dem Kestrel-Thread des Hubs; dort war
        /// <c>new WindowBase()</c> bis 2026-09-05 der zweite Fehler nach dem ersten, und weil die
        /// Rueckrufe <c>async void</c> sind, riss er die ganze Anwendung ab statt ein Fehlerfenster
        /// zu zeigen.
        /// </para>
        /// </summary>
        public static void HandleException(Exception ex)
        {
            if (ex == null)
                return;

            var dispatcher = Application.Current?.Dispatcher;

            // Ohne laufende Anwendung (Tests), auf dem richtigen Thread, oder wenn die Anwendung
            // gerade zumacht - dann nimmt der Dispatcher nichts mehr an und wuerde werfen.
            if (dispatcher == null || dispatcher.CheckAccess() || dispatcher.HasShutdownStarted)
            {
                Handler(ex);
                return;
            }

            // Nicht warten: der Hub-Thread soll weiterlaufen, waehrend das Fenster aufgeht.
            dispatcher.InvokeAsync(() => Handler(ex));
        }

        /// <summary>Das Meldungsfenster der App in der Art „Fehler“ (M29), ohne den Ablauf anzuhalten.</summary>
        private static void ShowExceptionWindow(Exception ex) => PromptView.ShowError(ex);
    }
}