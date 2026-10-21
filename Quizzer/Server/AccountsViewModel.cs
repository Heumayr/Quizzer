using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Logic.Controller.TypedControllers;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Quizzer.Server
{
    /// <summary>
    /// Kontenverwaltung fuer den Admin (Rueckfrage M9: „Admin in der WPF-App"). Ein Konto haengt
    /// an genau einem Mitspieler - einem bestehenden ohne Konto (etwa einem Gast, der jetzt ein
    /// Konto bekommt) oder einem neuen.
    /// </summary>
    public sealed class AccountsViewModel : ViewModelBase
    {
        private AccountListItem? selected;
        private Player? playerForNewAccount;
        private string message = string.Empty;

        public ObservableCollection<AccountListItem> Accounts { get; } = new();

        /// <summary>Mitspieler ohne Konto - sie koennen eines bekommen.</summary>
        public ObservableCollection<Player> PlayersWithoutAccount { get; } = new();

        public AccountListItem? Selected
        {
            get => selected;
            set { selected = value; OnPropertyChanged(); }
        }

        public string NewUserName { get; set; } = string.Empty;

        public string NewPassword { get; set; } = string.Empty;

        public string NewPlayerName { get; set; } = string.Empty;

        public bool NewIsAdmin { get; set; }

        /// <summary><c>null</c> heisst: es entsteht ein neuer Mitspieler mit <see cref="NewPlayerName"/>.</summary>
        public Player? PlayerForNewAccount
        {
            get => playerForNewAccount;
            set { playerForNewAccount = value; OnPropertyChanged(); }
        }

        public string ResetPassword { get; set; } = string.Empty;

        public string Message
        {
            get => message;
            private set { message = value; OnPropertyChanged(); }
        }

        protected override Task OnloadAsync() => ReloadAsync();

        public override Task VMSaveAsync() => Task.CompletedTask;

        private async Task ReloadAsync()
        {
            if (ServerSession.Api is not { } api)
                return;

            try
            {
                var accounts = await api.GetAccountsAsync();
                Accounts.Clear();
                foreach (var a in accounts)
                    Accounts.Add(a);

                using var players = new PlayersController();
                var withAccount = accounts.Select(a => a.PlayerId).ToHashSet();

                PlayersWithoutAccount.Clear();
                foreach (var p in (await players.GetAllAsync()).Where(p => !withAccount.Contains(p.Id)).OrderBy(p => p.CalculatedDisplayName))
                    PlayersWithoutAccount.Add(p);
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException)
            {
                Message = ex.Message;
            }
        }

        private AsyncRelayCommand? createCommand;

        public ICommand CreateCommand => createCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (ServerSession.Api is not { } api)
                return;

            try
            {
                var created = await api.CreateAccountAsync(new CreateAccountRequest(
                    NewUserName.Trim(),
                    NewPassword,
                    NewIsAdmin ? AccountRole.Admin : AccountRole.Member,
                    PlayerForNewAccount?.Id,
                    PlayerForNewAccount == null ? (string.IsNullOrWhiteSpace(NewPlayerName) ? NewUserName.Trim() : NewPlayerName.Trim()) : null));

                Message = $"Konto „{created.UserName}“ angelegt. Das Kennwort bitte persönlich weitergeben, nicht per Mail.";

                // Ein neuer Mitspieler entsteht am Server - erst das Abholen bringt ihn hierher.
                if (PlayerForNewAccount == null && ServerSession.Sync is { } sync)
                    await sync.PullAsync();

                await ReloadAsync();
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException)
            {
                Message = ex.Message;
            }
        });

        private AsyncRelayCommand? resetPasswordCommand;

        public ICommand ResetPasswordCommand => resetPasswordCommand ??= new AsyncRelayCommand(async _ =>
        {
            await UpdateSelectedAsync(new UpdateAccountRequest(ResetPassword, null, null), "Kennwort gesetzt, alle Sitzungen des Kontos beendet.");
        });

        private AsyncRelayCommand? toggleDisabledCommand;

        public ICommand ToggleDisabledCommand => toggleDisabledCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (Selected is { } account)
                await UpdateSelectedAsync(new UpdateAccountRequest(null, null, !account.IsDisabled), account.IsDisabled ? "Konto entsperrt." : "Konto gesperrt, alle Sitzungen beendet.");
        });

        private AsyncRelayCommand? toggleAdminCommand;

        public ICommand ToggleAdminCommand => toggleAdminCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (Selected is { } account)
            {
                var role = account.Role == AccountRole.Admin ? AccountRole.Member : AccountRole.Admin;
                await UpdateSelectedAsync(new UpdateAccountRequest(null, role, null), role == AccountRole.Admin ? "Jetzt Admin." : "Jetzt Mitglied.");
            }
        });

        private async Task UpdateSelectedAsync(UpdateAccountRequest request, string done)
        {
            if (ServerSession.Api is not { } api || Selected is not { } account)
            {
                Message = "Erst ein Konto in der Liste wählen.";
                return;
            }

            try
            {
                await api.UpdateAccountAsync(account.AccountId, request);
                Message = done;
                await ReloadAsync();
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException)
            {
                Message = ex.Message;
            }
        }
    }
}
