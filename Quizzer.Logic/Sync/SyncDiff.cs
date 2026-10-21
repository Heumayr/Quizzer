using Quizzer.DataModels.Transfer.Sync;

namespace Quizzer.Logic.Sync
{
    /// <summary>
    /// Was der Client seit dem letzten Abgleich geaendert hat: der Vergleich seiner aktuellen
    /// Zeilen mit den Hashes, die er beim Abholen bekommen hat (<paramref name="manifest"/>).
    /// <list type="bullet">
    /// <item>neuer Hash oder unbekannter Schluessel → Upsert (mit dem alten Hash als Grundlage)</item>
    /// <item>im Manifest, aber lokal verschwunden → Loeschung</item>
    /// </list>
    /// </summary>
    public static class SyncDiff
    {
        public static SyncPushRequest Compute(
            IReadOnlyCollection<SyncRow> current,
            IReadOnlyDictionary<SyncKey, string> manifest)
        {
            var request = new SyncPushRequest();
            var seen = new HashSet<SyncKey>();

            foreach (var row in current)
            {
                // Bei einer Frage, deren Typ sich geaendert hat, ist der Schluessel ein anderer;
                // die Grundlage ist dann der Hash unter dem alten Typ.
                var baseKey = FindBaseKey(row, manifest);
                seen.Add(baseKey ?? row.Key);

                if (baseKey != null && manifest[baseKey.Value] == row.Hash)
                    continue;

                request.Upserts.Add(new SyncUpsert
                {
                    Row = row,
                    BaseHash = baseKey == null ? null : manifest[baseKey.Value],
                });
            }

            foreach (var (key, hash) in manifest)
            {
                if (!seen.Contains(key))
                    request.Deletes.Add(new SyncDelete { Type = key.Type, Id = key.Id, BaseHash = hash });
            }

            return request;
        }

        private static SyncKey? FindBaseKey(SyncRow row, IReadOnlyDictionary<SyncKey, string> manifest)
        {
            if (manifest.ContainsKey(row.Key))
                return row.Key;

            if (!SyncModel.TryGet(row.Type, out var entry) || !SyncModel.IsQuestion(entry))
                return null;

            foreach (var other in SyncModel.Entries.Where(SyncModel.IsQuestion))
            {
                var key = new SyncKey(other.Name, row.Id);

                if (manifest.ContainsKey(key))
                    return key;
            }

            return null;
        }
    }
}
