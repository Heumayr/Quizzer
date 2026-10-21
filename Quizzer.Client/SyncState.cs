using Quizzer.DataModels.Transfer.Sync;
using System.Text.Json;

namespace Quizzer.Client
{
    /// <summary>
    /// Was der Client ueber den Server weiss: je Zeile der Hash beim letzten Abgleich
    /// (<see cref="Manifest"/>) und die Zeilen, die der Server abgewiesen hat
    /// (<see cref="Suppressed"/>) - die werden nicht bei jedem Push erneut geschickt, solange
    /// sie sich lokal nicht wieder aendern.
    /// </summary>
    public sealed class SyncState
    {
        public Guid AccountId { get; set; }

        public DateTimeOffset? LastPullAt { get; set; }

        public DateTimeOffset? LastPushAt { get; set; }

        public Dictionary<SyncKey, string> Manifest { get; set; } = new();

        public Dictionary<SyncKey, string> Suppressed { get; set; } = new();
    }

    public interface ISyncStateStore
    {
        SyncState? Load();

        void Save(SyncState state);
    }

    public sealed class MemorySyncStateStore : ISyncStateStore
    {
        private string? json;

        public SyncState? Load() => json == null ? null : FileSyncStateStore.Deserialize(json);

        public void Save(SyncState state) => json = FileSyncStateStore.Serialize(state);
    }

    /// <summary>Eine JSON-Datei je Server und Konto. Schreibt erst in eine Nebendatei und tauscht dann.</summary>
    public sealed class FileSyncStateStore(string path) : ISyncStateStore
    {
        private sealed record Entry(string Type, Guid Id, string Hash);

        private sealed record Document(Guid AccountId, DateTimeOffset? LastPullAt, DateTimeOffset? LastPushAt, List<Entry> Manifest, List<Entry> Suppressed);

        public string Path { get; } = path;

        public SyncState? Load()
        {
            if (!File.Exists(Path))
                return null;

            try
            {
                return Deserialize(File.ReadAllText(Path));
            }
            catch (JsonException)
            {
                // Eine kaputte Zustandsdatei heisst nur: beim naechsten Abholen wird alles neu verglichen.
                return null;
            }
        }

        public void Save(SyncState state)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temp = Path + ".tmp";
            File.WriteAllText(temp, Serialize(state));
            File.Move(temp, Path, overwrite: true);
        }

        internal static string Serialize(SyncState state) => JsonSerializer.Serialize(new Document(
            state.AccountId, state.LastPullAt, state.LastPushAt,
            state.Manifest.Select(m => new Entry(m.Key.Type, m.Key.Id, m.Value)).ToList(),
            state.Suppressed.Select(m => new Entry(m.Key.Type, m.Key.Id, m.Value)).ToList()));

        internal static SyncState Deserialize(string json)
        {
            var document = JsonSerializer.Deserialize<Document>(json) ?? throw new JsonException("leer");

            return new SyncState
            {
                AccountId = document.AccountId,
                LastPullAt = document.LastPullAt,
                LastPushAt = document.LastPushAt,
                Manifest = document.Manifest.ToDictionary(e => new SyncKey(e.Type, e.Id), e => e.Hash),
                Suppressed = document.Suppressed.ToDictionary(e => new SyncKey(e.Type, e.Id), e => e.Hash),
            };
        }
    }
}
