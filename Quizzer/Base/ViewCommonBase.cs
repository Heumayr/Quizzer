using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Quizzer.Base
{
    public abstract class ViewCommonBase
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Nur fuer Tests: liefert den zu verwendenden <see cref="IUiInvoker"/>. Ist der Wert
        /// <c>null</c> (Regelfall), wird der WPF-Dispatcher benutzt.
        /// </summary>
        internal static Func<IUiInvoker>? UiInvokerOverride { get; set; }

        private IUiInvoker? uiInvoker;

        /// <summary>
        /// Der Weg auf den Oberflaechen-Thread. Wird erst beim ersten Zugriff aufgeloest - nicht
        /// im Konstruktor, sonst koennte kein ViewModel ohne laufende Anwendung entstehen.
        /// </summary>
        protected IUiInvoker Ui => uiInvoker ??= UiInvokerOverride?.Invoke()
            ?? new DispatcherUiInvoker(Application.Current.Dispatcher);

        protected void RunOnUi(Action action) => Ui.Run(action);

        protected Task RunOnUiAsync(Action action) => Ui.RunAsync(action);

        protected Task RunOnUiAsync(Func<Task> action) => Ui.RunAsync(action);
    }
}