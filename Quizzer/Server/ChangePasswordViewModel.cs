using Quizzer.Base;
using Quizzer.Client;
using System.Windows.Input;

namespace Quizzer.Server
{
    /// <summary>Das eigene Kennwort aendern. Der Server beendet dabei alle anderen Sitzungen.</summary>
    public sealed class ChangePasswordViewModel : ViewModelBase
    {
        private string message = string.Empty;

        public string Current { get; set; } = string.Empty;

        public string Next { get; set; } = string.Empty;

        public string Repeat { get; set; } = string.Empty;

        public string Message
        {
            get => message;
            private set { message = value; OnPropertyChanged(); }
        }

        protected override Task OnloadAsync() => Task.CompletedTask;

        public override Task VMSaveAsync() => Task.CompletedTask;

        private AsyncRelayCommand? saveCommand;

        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (Next != Repeat)
            {
                Message = "Die beiden neuen Kennwörter stimmen nicht überein.";
                return;
            }

            if (ServerSession.Api is not { } api || ServerSession.Account is not { } account)
                return;

            try
            {
                await api.ChangePasswordAsync(Current, Next);

                // Das Aendern widerruft am Server alle Erneuerungsschluessel - auch den eigenen.
                // Also gleich mit dem neuen Kennwort wieder anmelden.
                await api.LoginAsync(account.UserName, Next);

                UserPrompt.Inform("Das Kennwort ist geändert. Andere Sitzungen deines Kontos sind beendet.", "Kennwort");
                Window?.Close();
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException)
            {
                Message = ex.Message;
            }
        });
    }
}
