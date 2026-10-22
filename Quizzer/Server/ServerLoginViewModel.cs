using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Server
{
    /// <summary>
    /// Die Anmeldung am Quizzer-Server (M9: „ein vorgeschaltetes sicheres login ... so das sich
    /// das wpf mit user anmeldet"). Erst wird versucht, die letzte Sitzung still fortzusetzen;
    /// gelingt das nicht, fragt die Maske nach Name und Kennwort. Ist der Server nicht
    /// erreichbar, kann man mit dem Stand der letzten Anmeldung offline weiterarbeiten.
    /// </summary>
    public sealed class ServerLoginViewModel : ViewModelBase
    {
        private string userName = string.Empty;
        private string password = string.Empty;
        private string error = string.Empty;
        private string busy = string.Empty;
        private bool serverUnreachable;
        private Uri? server;

        public bool SignedIn { get; private set; }

        public string ServerText => server == null ? "(keine Adresse)" : server.GetLeftPart(UriPartial.Authority);

        public string UserName
        {
            get => userName;
            set { userName = value; Error = string.Empty; OnPropertyChanged(); }
        }

        public string Password
        {
            get => password;
            set { password = value; Error = string.Empty; OnPropertyChanged(); }
        }

        public string Error
        {
            get => error;
            private set { error = value; OnPropertyChanged(); OnPropertyChanged(nameof(ErrorVisibility)); }
        }

        public Visibility ErrorVisibility => string.IsNullOrEmpty(Error) ? Visibility.Collapsed : Visibility.Visible;

        public string Busy
        {
            get => busy;
            private set
            {
                busy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BusyVisibility));
                OnPropertyChanged(nameof(FormVisibility));
                OnPropertyChanged(nameof(OfflineVisibility));
            }
        }

        public Visibility BusyVisibility => string.IsNullOrEmpty(Busy) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility FormVisibility => string.IsNullOrEmpty(Busy) ? Visibility.Visible : Visibility.Collapsed;

        public Visibility OfflineVisibility =>
            serverUnreachable && string.IsNullOrEmpty(Busy) && server != null && ServerSession.CanWorkOffline(server)
                ? Visibility.Visible : Visibility.Collapsed;

        protected override async Task OnloadAsync()
        {
            UserName = UserSettings.Load().LastUserName ?? string.Empty;

            QuizzerApiClient api;
            try
            {
                api = ServerSession.CreateApi();
                server = api.BaseAddress;
                OnPropertyChanged(nameof(ServerText));
            }
            catch (ArgumentException ex)
            {
                Error = ex.Message + " Bitte in den Einstellungen korrigieren.";
                return;
            }

            Busy = "Sitzung wird fortgesetzt …";
            try
            {
                if (await api.TryResumeAsync())
                {
                    await CompleteAsync(api);
                    return;
                }
            }
            catch (ServerUnreachableException ex)
            {
                SetUnreachable(ex.Message);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
            finally
            {
                if (!SignedIn)
                    Busy = string.Empty;
            }
        }

        public override Task VMSaveAsync() => Task.CompletedTask;

        private AsyncRelayCommand? signInCommand;

        public ICommand SignInCommand => signInCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (ServerSession.Api == null)
                return;

            if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrEmpty(Password))
            {
                Error = "Benutzername und Kennwort eingeben.";
                return;
            }

            Busy = "Anmeldung …";
            try
            {
                await ServerSession.Api.LoginAsync(UserName.Trim(), Password);
                Password = string.Empty;
                await CompleteAsync(ServerSession.Api);
            }
            catch (ServerUnreachableException ex)
            {
                SetUnreachable(ex.Message);
            }
            catch (ApiException ex)
            {
                Error = ex.Message;
            }
            catch (Exception ex)
            {
                Error = "Die Anmeldung ist gescheitert: " + ex.Message;
            }
            finally
            {
                if (!SignedIn)
                    Busy = string.Empty;
            }
        });

        private AsyncRelayCommand? offlineCommand;

        public ICommand OfflineCommand => offlineCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (server == null)
                return;

            Busy = "Lokaler Stand wird geöffnet …";
            try
            {
                if (await ServerSession.ActivateOfflineAsync(server))
                {
                    SignedIn = true;
                    UserPrompt.Inform(
                        "Du arbeitest ohne Server mit dem Stand deiner letzten Anmeldung. "
                        + "Alles wird lokal gespeichert. Sobald der Server wieder erreichbar ist, "
                        + "wird von selbst übertragen.",
                        "Offline");
                    Window?.Close();
                    return;
                }

                Error = "Auf diesem Rechner gibt es keinen Stand für offline.";
            }
            finally
            {
                if (!SignedIn)
                    Busy = string.Empty;
            }
        });

        private RelayCommand? settingsCommand;

        public ICommand SettingsCommand => settingsCommand ??= new RelayCommand(_ =>
        {
            new Views.SettingsView().ShowDialog();
            _ = OnloadAsync();
        });

        private RelayCommand? cancelCommand;

        public ICommand CancelCommand => cancelCommand ??= new RelayCommand(_ =>
        {
            SignedIn = false;
            Window?.Close();
        });

        private async Task CompleteAsync(QuizzerApiClient api)
        {
            var progress = new Progress<string>(text => Busy = text);
            var result = await ServerSession.ActivateAsync(api, progress);

            SignedIn = true;

            if (result.MediaProblems.Count > 0)
            {
                UserPrompt.Inform(
                    "Einige Medien wurden nicht übernommen:" + Environment.NewLine
                    + string.Join(Environment.NewLine, result.MediaProblems.Take(10)),
                    "Abgleich");
            }

            Window?.Close();
        }

        private void SetUnreachable(string message)
        {
            serverUnreachable = true;
            Error = message;
            OnPropertyChanged(nameof(OfflineVisibility));
        }
    }
}
