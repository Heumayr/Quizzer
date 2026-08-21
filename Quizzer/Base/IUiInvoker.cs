using System.Windows.Threading;

namespace Quizzer.Base
{
    /// <summary>
    /// Fuehrt eine Aktion auf dem Oberflaechen-Thread aus. Ersetzt den direkten Zugriff auf
    /// <c>Application.Current.Dispatcher</c>, damit ViewModels auch ohne laufende
    /// <see cref="System.Windows.Application"/> erzeugt und geprueft werden koennen.
    /// </summary>
    public interface IUiInvoker
    {
        /// <summary>Fuehrt die Aktion aus - sofort, wenn der aufrufende Thread bereits der richtige ist.</summary>
        void Run(Action action);

        /// <summary>Fuehrt die Aktion aus und liefert eine Aufgabe, die deren Ende anzeigt.</summary>
        Task RunAsync(Action action);

        /// <summary>Fuehrt die asynchrone Aktion aus und reicht deren Aufgabe durch.</summary>
        Task RunAsync(Func<Task> action);
    }

    /// <summary>
    /// Der Regelfall im laufenden Programm: leitet an den WPF-<see cref="Dispatcher"/> weiter.
    /// Verhaelt sich genau wie der frueher fest eingebaute Zugriff.
    /// </summary>
    public sealed class DispatcherUiInvoker : IUiInvoker
    {
        private readonly Dispatcher dispatcher;

        public DispatcherUiInvoker(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public void Run(Action action)
        {
            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }

        public Task RunAsync(Action action)
        {
            if (dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action).Task;
        }

        public Task RunAsync(Func<Task> action)
        {
            if (dispatcher.CheckAccess())
                return action();

            return dispatcher.InvokeAsync(action).Task.Unwrap();
        }
    }

    /// <summary>
    /// Fuehrt alles an Ort und Stelle aus, ohne Dispatcher. Fuer Tests, die keine
    /// <see cref="System.Windows.Application"/> starten - und damit auch ohne Thread-Bindung,
    /// die ein fortgesetztes <c>await</c> zum Stillstand bringen wuerde.
    /// </summary>
    public sealed class InlineUiInvoker : IUiInvoker
    {
        public void Run(Action action) => action();

        public Task RunAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task RunAsync(Func<Task> action) => action();
    }
}
