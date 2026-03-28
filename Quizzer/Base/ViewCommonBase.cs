using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace Quizzer.Base
{
    public abstract class ViewCommonBase
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected Dispatcher UiDispatcher { get; } = Application.Current.Dispatcher;

        protected void RunOnUi(Action action)
        {
            if (UiDispatcher.CheckAccess())
            {
                action();
                return;
            }

            UiDispatcher.Invoke(action);
        }

        protected Task RunOnUiAsync(Action action)
        {
            if (UiDispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return UiDispatcher.InvokeAsync(action).Task;
        }

        protected Task RunOnUiAsync(Func<Task> action)
        {
            if (UiDispatcher.CheckAccess())
                return action();

            return UiDispatcher.InvokeAsync(action).Task.Unwrap();
        }
    }
}