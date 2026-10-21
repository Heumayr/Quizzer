using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Context;

namespace Quizzer.Client
{
    public enum SyncStatus
    {
        /// <summary>Alles beim Server.</summary>
        UpToDate,

        /// <summary>Lokal gespeichert, wird gleich geschickt.</summary>
        Pending,

        /// <summary>Wird gerade geschickt.</summary>
        Pushing,

        /// <summary>Server nicht erreichbar - wird wiederholt, lokal geht nichts verloren.</summary>
        Offline,

        /// <summary>Sitzung abgelaufen - Anmeldung noetig.</summary>
        SignedOut,

        /// <summary>Der Server hat Aenderungen abgewiesen.</summary>
        Rejected,
    }

    public sealed record SyncStatusInfo(SyncStatus Status, DateTimeOffset? LastSuccess, string Message, IReadOnlyList<SyncRejected> Rejected);

    /// <summary>
    /// Schickt Aenderungen nebenher zum Server (M9: „speicherungen werden nebenbei immer an den
    /// server uebermittelt dass der spielstand geladen werden kann").
    /// <list type="bullet">
    /// <item>Haengt an <see cref="DataChangeNotifier.Saved"/> - jedes Speichern, egal wo.</item>
    /// <item>Wartet <see cref="Debounce"/> ab, damit eine Folge von Speichervorgaengen (ein
    /// Spielzug schreibt mehrmals) als ein Push hinausgeht.</item>
    /// <item>Ohne Netz: lokal geht nichts verloren; wiederholt wird mit wachsendem Abstand.</item>
    /// </list>
    /// </summary>
    public sealed class BackgroundSync : IDisposable
    {
        private readonly ClientSyncService sync;
        private readonly object sync_ = new();
        private readonly CancellationTokenSource stop = new();
        private Timer? timer;
        private bool running;
        private bool again;
        private TimeSpan retryDelay;

        public BackgroundSync(ClientSyncService sync, TimeSpan? debounce = null)
        {
            this.sync = sync;
            Debounce = debounce ?? TimeSpan.FromSeconds(3);
            retryDelay = MinRetry;
            Status = new SyncStatusInfo(SyncStatus.UpToDate, null, "Abgeglichen.", []);
        }

        public static readonly TimeSpan MinRetry = TimeSpan.FromSeconds(15);

        public static readonly TimeSpan MaxRetry = TimeSpan.FromMinutes(5);

        public TimeSpan Debounce { get; }

        public SyncStatusInfo Status { get; private set; }

        /// <summary>Wird auf einem Hintergrund-Thread ausgeloest.</summary>
        public event EventHandler<SyncStatusInfo>? StatusChanged;

        public void Start()
        {
            DataChangeNotifier.Saved += OnSaved;
            Schedule(Debounce);
        }

        private void OnSaved(object? sender, EventArgs e)
        {
            if (Status.Status != SyncStatus.Pushing)
                SetStatus(SyncStatus.Pending, "Lokal gespeichert, wird übertragen …");

            Schedule(Debounce);
        }

        private void Schedule(TimeSpan delay)
        {
            lock (sync_)
            {
                if (stop.IsCancellationRequested)
                    return;

                timer ??= new Timer(_ => _ = RunAsync(), null, Timeout.Infinite, Timeout.Infinite);
                timer.Change(delay, Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>Jetzt sofort schicken und abwarten - etwa vor dem Beenden.</summary>
        public Task FlushAsync() => RunAsync();

        private async Task RunAsync()
        {
            lock (sync_)
            {
                if (running)
                {
                    again = true;
                    return;
                }

                running = true;
            }

            try
            {
                do
                {
                    lock (sync_) again = false;
                    await PushOnceAsync();
                }
                while (Volatile.Read(ref again) && !stop.IsCancellationRequested);
            }
            finally
            {
                lock (sync_) running = false;
            }
        }

        private async Task PushOnceAsync()
        {
            SetStatus(SyncStatus.Pushing, "Wird übertragen …");

            try
            {
                var result = await sync.PushAsync(stop.Token);
                retryDelay = MinRetry;

                if (result.Rejected.Count > 0)
                {
                    SetStatus(SyncStatus.Rejected,
                        $"{result.Rejected.Count} Änderung(en) hat der Server abgewiesen: "
                        + string.Join("; ", result.Rejected.Take(3).Select(r => r.Message)),
                        result.Rejected, success: true);
                }
                else
                {
                    SetStatus(SyncStatus.UpToDate, "Abgeglichen.", success: true);
                }
            }
            catch (SessionExpiredException)
            {
                SetStatus(SyncStatus.SignedOut, "Die Sitzung ist abgelaufen. Lokal ist alles gespeichert; nach dem Anmelden wird übertragen.");
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (ex is ServerUnreachableException or ApiException or IOException)
            {
                SetStatus(SyncStatus.Offline, $"Server nicht erreichbar - lokal ist alles gespeichert. Neuer Versuch in {retryDelay.TotalSeconds:0} s. ({ex.Message})");
                Schedule(retryDelay);
                retryDelay = TimeSpan.FromTicks(Math.Min(retryDelay.Ticks * 2, MaxRetry.Ticks));
            }
        }

        private void SetStatus(SyncStatus status, string message, IReadOnlyList<SyncRejected>? rejected = null, bool success = false)
        {
            Status = new SyncStatusInfo(status, success ? DateTimeOffset.Now : Status.LastSuccess, message, rejected ?? []);
            StatusChanged?.Invoke(this, Status);
        }

        public void Dispose()
        {
            DataChangeNotifier.Saved -= OnSaved;
            stop.Cancel();
            lock (sync_)
            {
                timer?.Dispose();
                timer = null;
            }
            stop.Dispose();
        }
    }
}
