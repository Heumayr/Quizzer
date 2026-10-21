using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Sync;
using System.Text.Json;

namespace Quizzer.Client
{
    public sealed record PullResult(int Changed, int Deleted, int MediaDownloaded, IReadOnlyList<string> MediaProblems);

    public sealed record PushResult(int Accepted, int Deleted, IReadOnlyList<SyncRejected> Rejected, int MediaUploaded, IReadOnlyList<string> MediaProblems)
    {
        public static readonly PushResult Nothing = new(0, 0, [], 0, []);

        public bool HadChanges => Accepted + Deleted + Rejected.Count > 0;
    }

    /// <summary>
    /// Der Abgleich auf Seite des WPF (M9: „vorerst sollen alle daten zu beginn geladen werden
    /// damit es ganz lokal laufen kann und nur sicherungen spielstaende zurueckgeschickt werden").
    /// <list type="bullet">
    /// <item><see cref="PullAsync"/>: alles, was das Konto sehen darf, in die lokale Datenbank -
    /// danach arbeitet das Programm genau wie ohne Server.</item>
    /// <item><see cref="PushAsync"/>: was sich lokal seit dem letzten Abgleich geaendert hat,
    /// zurueck an den Server. Laeuft im Hintergrund nach jedem Speichern.</item>
    /// </list>
    /// Abholen und Zurueckschicken laufen nie gleichzeitig.
    /// </summary>
    public sealed class ClientSyncService(
        QuizzerApiClient api,
        Func<SyncStore> openStore,
        ISyncStateStore stateStore,
        string? mediaRoot)
    {
        private readonly SemaphoreSlim gate = new(1, 1);

        public QuizzerApiClient Api => api;

        /// <summary>
        /// Holt den vollstaendigen Stand und spielt ihn lokal ein. <b>Vorher wird zurueckgeschickt</b>,
        /// was lokal noch offen ist - sonst ueberschriebe das Abholen eigene Aenderungen.
        /// </summary>
        public async Task<PullResult> PullAsync(CancellationToken ct = default)
        {
            await gate.WaitAsync(ct);
            try
            {
                var state = LoadState();

                if (state.Manifest.Count > 0)
                    await PushCoreAsync(state, ct);

                var snapshot = await api.GetSnapshotAsync(ct);
                if (snapshot.FormatVersion != SyncSnapshot.CurrentFormatVersion)
                    throw new InvalidOperationException($"Der Server liefert Abgleichsformat {snapshot.FormatVersion}, dieses Programm kennt {SyncSnapshot.CurrentFormatVersion}. Bitte aktualisieren.");

                int changed, deleted;
                await using (var store = openStore())
                {
                    var local = (await store.ReadAllAsync(ct)).ToDictionary(r => r.Key);
                    var remote = snapshot.Rows.ToDictionary(r => r.Key);

                    var upserts = snapshot.Rows.Where(r => !local.TryGetValue(r.Key, out var l) || l.Hash != r.Hash).ToList();

                    // Lokal vorhanden, am Server nicht (mehr) sichtbar. Eine Frage, die am Server nur
                    // den Typ gewechselt hat, ist kein Loeschfall - die Umwandlung erledigt ApplyAsync.
                    var remoteIds = remote.Keys.Select(k => k.Id).ToHashSet();
                    var deletes = local.Keys.Where(k => !remote.ContainsKey(k) && !remoteIds.Contains(k.Id)).ToList();

                    await store.ApplyAsync(upserts, deletes, strict: false, cancellationToken: ct);
                    changed = upserts.Count;
                    deleted = deletes.Count;
                }

                state.Manifest = snapshot.Rows.ToDictionary(r => r.Key, r => r.Hash);
                state.Suppressed.Clear();
                state.LastPullAt = DateTimeOffset.UtcNow;
                stateStore.Save(state);

                var (downloaded, problems) = await DownloadMediaAsync(ct);
                return new PullResult(changed, deleted, downloaded, problems);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>Schickt die lokalen Aenderungen zurueck. Ohne Aenderungen geht nichts uebers Netz.</summary>
        public async Task<PushResult> PushAsync(CancellationToken ct = default)
        {
            await gate.WaitAsync(ct);
            try
            {
                var state = LoadState();
                return await PushCoreAsync(state, ct);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>Ob es lokal etwas gibt, das der Server noch nicht hat.</summary>
        public async Task<int> CountPendingAsync(CancellationToken ct = default)
        {
            var state = LoadState();
            var request = await BuildRequestAsync(state, ct);
            return request.Upserts.Count + request.Deletes.Count;
        }

        private async Task<SyncPushRequest> BuildRequestAsync(SyncState state, CancellationToken ct)
        {
            List<SyncRow> local;
            await using (var store = openStore())
                local = await store.ReadAllAsync(ct);

            var request = SyncDiff.Compute(local, state.Manifest);

            // Was der Server abgewiesen hat, geht erst wieder mit, wenn es sich lokal erneut aendert.
            request.Upserts.RemoveAll(u => state.Suppressed.TryGetValue(u.Row.Key, out var hash) && hash == u.Row.Hash);
            request.Deletes.RemoveAll(d => state.Suppressed.TryGetValue(d.Key, out var hash) && hash == DeletedMarker);

            return request;
        }

        private const string DeletedMarker = "(gelöscht)";

        private async Task<PushResult> PushCoreAsync(SyncState state, CancellationToken ct)
        {
            var request = await BuildRequestAsync(state, ct);

            if (request.Upserts.Count == 0 && request.Deletes.Count == 0)
                return PushResult.Nothing;

            var (uploaded, mediaProblems) = await UploadMediaAsync(request.Upserts.Select(u => u.Row), ct);

            var response = await api.PushAsync(request, ct);

            foreach (var accepted in response.Accepted)
            {
                var key = new SyncKey(accepted.Type, accepted.Id);

                // Eine Frage mit neuem Typ steht unter einem neuen Schluessel; der alte faellt weg.
                foreach (var stale in state.Manifest.Keys.Where(k => k.Id == key.Id && k != key).ToList())
                    state.Manifest.Remove(stale);

                state.Manifest[key] = accepted.Hash;
                state.Suppressed.Remove(key);
            }

            foreach (var deleted in response.Deleted)
            {
                state.Manifest.Remove(deleted);
                state.Suppressed.Remove(deleted);
            }

            foreach (var rejected in response.Rejected)
            {
                var key = new SyncKey(rejected.Type, rejected.Id);
                var upsert = request.Upserts.FirstOrDefault(u => u.Row.Key == key);
                state.Suppressed[key] = upsert?.Row.Hash ?? DeletedMarker;
            }

            state.LastPushAt = DateTimeOffset.UtcNow;
            stateStore.Save(state);

            return new PushResult(response.Accepted.Count, response.Deleted.Count, response.Rejected, uploaded, mediaProblems);
        }

        private SyncState LoadState()
        {
            var accountId = api.Account?.AccountId ?? throw new InvalidOperationException("Nicht angemeldet.");
            var state = stateStore.Load();

            // Ein Zustand eines anderen Kontos gilt hier nicht - dann wird alles neu verglichen.
            return state != null && state.AccountId == accountId ? state : new SyncState { AccountId = accountId };
        }

        private async Task<(int Count, List<string> Problems)> DownloadMediaAsync(CancellationToken ct)
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(mediaRoot))
                return (0, problems);

            var count = 0;

            foreach (var file in await api.GetMediaManifestAsync(ct))
            {
                if (!MediaPaths.TryNormalize(file.Path, out var normalized))
                    continue;

                var full = MediaPaths.ToFullPath(mediaRoot, normalized);
                if (full == null)
                    continue;

                if (File.Exists(full))
                {
                    // Nie ueberschreiben: eine gleichnamige lokale Datei ist eine eigene.
                    if (new FileInfo(full).Length != file.Size)
                        problems.Add($"{normalized}: liegt lokal schon mit anderem Inhalt, nicht ersetzt.");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                var temp = full + ".download";

                await using (var output = File.Create(temp))
                {
                    if (!await api.DownloadMediaAsync(normalized, output, ct))
                    {
                        problems.Add($"{normalized}: vom Server nicht herausgegeben.");
                        continue;
                    }
                }

                File.Move(temp, full);
                count++;
            }

            return (count, problems);
        }

        private async Task<(int Count, List<string> Problems)> UploadMediaAsync(IEnumerable<SyncRow> rows, CancellationToken ct)
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(mediaRoot))
                return (0, problems);

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                switch (row.Type)
                {
                    case nameof(QuestionStepResource):
                        AddIfSet(paths, row, nameof(QuestionStepResource.ResourceFileName), MediaPaths.ForResource);
                        break;
                    case nameof(RevealQuestion):
                        AddIfSet(paths, row, nameof(RevealQuestion.ImageFileName), MediaPaths.ForResource);
                        break;
                    case nameof(Player):
                        AddIfSet(paths, row, nameof(Player.UserPictureFileName), name => name);
                        break;
                    case nameof(GameTheme):
                        if (GetString(row, nameof(GameTheme.FolderName)) is { Length: > 0 } folder)
                        {
                            var directory = Path.Combine(mediaRoot, MediaPaths.ThemesFolder, folder);
                            if (Directory.Exists(directory))
                            {
                                foreach (var file in Directory.EnumerateFiles(directory))
                                    paths.Add(MediaPaths.ForTheme(folder, Path.GetFileName(file)));
                            }
                        }
                        break;
                }
            }

            var count = 0;

            foreach (var path in paths)
            {
                if (!MediaPaths.TryNormalize(path, out var normalized))
                    continue;

                var full = MediaPaths.ToFullPath(mediaRoot, normalized);
                if (full == null || !File.Exists(full))
                    continue;

                if (await api.UploadMediaAsync(normalized, () => File.OpenRead(full), ct))
                    count++;
                else
                    problems.Add($"{normalized}: am Server liegt unter diesem Namen schon eine andere Datei.");
            }

            return (count, problems);
        }

        private static void AddIfSet(HashSet<string> paths, SyncRow row, string field, Func<string, string> toPath)
        {
            if (GetString(row, field) is { Length: > 0 } name)
                paths.Add(toPath(name));
        }

        private static string? GetString(SyncRow row, string field)
            => row.Values.TryGetValue(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
