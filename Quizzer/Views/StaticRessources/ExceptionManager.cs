using Quizzer.Base;
using Quizzer.Views.HelperViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;

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

        private static void ShowExceptionWindow(Exception ex)
        {
            var window = new WindowBase();

            var contentPanel = new StackPanel();

            _ = RenderException(contentPanel, ex);

            var rtb = new RichTextBox();
            rtb.IsReadOnly = true;

            var binding = new Binding(nameof(ex.StackTrace))
            {
                Source = ex,                 // or a specific vm instance
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.Default
            };

            BindingOperations.SetBinding(rtb,
                                         RichTextBoxBinder.PlainTextProperty,
                                         binding);

            contentPanel.Children.Add(rtb);

            contentPanel.Children.Add(new Button()
            {
                Content = "OK",
                Command = new RelayCommand((p) => window.Close())
            });

            var scrollViewer = new ScrollViewer();
            scrollViewer.Content = contentPanel;

            window.Content = scrollViewer;

            window.Show();
        }

        private static object RenderException(StackPanel pnl, Exception ex)
        {
            var view = new ExceptionView();
            view.Message.Content = ex.Message;
            view.Source.Content = ex.Source;
            pnl.Children.Add(view);

            if (ex.InnerException != null)
            {
                return RenderException(pnl, ex.InnerException);
            }

            return ex;
        }
    }
}