using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels.Transfer.Sync;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Quizzer.Server
{
    /// <summary>
    /// V4 (2026-10-22): „N Änderungen nicht übertragen - warum?". Bisher stand der Grund einer
    /// Ablehnung einmal in der Statuszeile und war beim nächsten Durchlauf weg, die Änderung lag
    /// still liegen. Hier stehen sie alle, mit Grund und dem, was man tun kann.
    /// </summary>
    public sealed class SyncRejectionsViewModel : ViewModelBase
    {
        private string summary = "Wird geladen …";
        private bool busy;

        public sealed record Row(string Art, string Eintrag, string Grund, string Hilfe, string Meldung);

        public ObservableCollection<Row> Rows { get; } = new();

        public string Summary
        {
            get => summary;
            private set { summary = value; OnPropertyChanged(); }
        }

        protected override Task OnloadAsync() => LoadAsync();

        private async Task LoadAsync()
        {
            if (ServerSession.Sync is not { } sync)
            {
                Summary = "Nicht mit dem Server verbunden.";
                return;
            }

            var offen = await Task.Run(() => sync.GetRejectionsAsync());

            Rows.Clear();
            foreach (var r in offen)
            {
                var (grund, hilfe) = Erklaere(r.Reason);
                var eintrag = r.Loeschung ? "(lokal gelöscht)" : r.Bezeichnung.Length > 0 ? r.Bezeichnung : "-";
                Rows.Add(new Row(Art(r.Type), eintrag, grund, hilfe, r.Message));
            }

            Summary = offen.Count switch
            {
                0 => "Alles übertragen - der Server hat nichts abgewiesen.",
                1 => "1 Änderung hat der Server abgewiesen. Sie liegt nur auf diesem Rechner.",
                _ => $"{offen.Count} Änderungen hat der Server abgewiesen. Sie liegen nur auf diesem Rechner.",
            };

            OnPropertyChanged(nameof(HasRows));
        }

        public bool HasRows => Rows.Count > 0;

        private AsyncRelayCommand? resendCommand;

        public ICommand ResendCommand => resendCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (ServerSession.Sync is not { } sync)
                return;

            await RunAsync(async () =>
            {
                var result = await Task.Run(() => sync.ResendRejectedAsync());
                await LoadAsync();
                Summary = (result.Rejected.Count == 0 ? "Alles durchgegangen. " : $"Wieder abgewiesen: {result.Rejected.Count}. ") + Summary;
            });
        }, _ => !busy && HasRows);

        private AsyncRelayCommand? takeServerCommand;

        public ICommand TakeServerCommand => takeServerCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (ServerSession.Sync is not { } sync)
                return;

            if (!UserPrompt.Confirm(
                    "Den Stand des Servers übernehmen?"
                    + Environment.NewLine + Environment.NewLine
                    + "Die abgewiesenen Änderungen auf diesem Rechner gehen dabei verloren - sie werden "
                    + "durch den Stand des Servers ersetzt. Alles andere wird vorher übertragen.",
                    "Serverstand übernehmen"))
                return;

            await RunAsync(async () =>
            {
                await Task.Run(() => sync.PullAsync());
                await LoadAsync();
            });
        }, _ => !busy && HasRows);

        private async Task RunAsync(Func<Task> action)
        {
            busy = true;
            CommandManager.InvalidateRequerySuggested();

            try
            {
                await action();
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException or SyncSafetyException)
            {
                Summary = ex.Message;
            }
            finally
            {
                busy = false;
                ServerSession.Background?.Refresh();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>Was der Grund heißt und was dagegen hilft.</summary>
        internal static (string Grund, string Hilfe) Erklaere(SyncRejectReason? reason) => reason switch
        {
            SyncRejectReason.Conflict => (
                "Am Server inzwischen geändert",
                "Ein anderer Rechner war schneller. „Serverstand übernehmen\", dann die Änderung noch einmal machen."),
            SyncRejectReason.Forbidden => (
                "Keine Berechtigung",
                "Gehört einem anderen Konto - nur dessen Besitzer oder der Admin darf es ändern. „Serverstand übernehmen\"."),
            SyncRejectReason.Invalid => (
                "Vom Server als fehlerhaft abgewiesen",
                "Oft fehlte ein Eintrag, auf den dieser verweist. „Erneut senden\"; hilft das nicht, „Serverstand übernehmen\"."),
            _ => (
                "Grund unbekannt",
                "„Erneut senden\" zeigt den Grund, falls er wieder abgewiesen wird."),
        };

        /// <summary>Die Art eines Eintrags, wie man sie im Programm nennt.</summary>
        internal static string Art(string type) => type switch
        {
            "Game" => "Spiel",
            "Category" => "Kategorie",
            "QuestionStepResource" => "Frageschritt",
            "GameGridCoordinate" => "Spielfeld-Zelle",
            "QuestionResult" => "Ergebnis",
            "Player" => "Mitspieler",
            "PlayerXGame" => "Teilnahme",
            "GameTheme" => "Design",
            _ when type.EndsWith("Question", StringComparison.Ordinal) => "Frage",
            _ => type,
        };

        public override Task VMSaveAsync() => Task.CompletedTask;
    }
}
