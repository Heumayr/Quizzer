using Quizzer.DataModels.Transfer.Sync;
using System.Text.Json;

namespace Quizzer.Client
{
    /// <summary>
    /// V17: was das Verwerfen einer aufgehaltenen Massenlöschung lokal tut, rein gerechnet aus lokalem Stand, Serverstand
    /// und Manifest. Zurück kommen die gelöschten Fragen und Spiele samt dem lokal Gelöschten, das an ihnen hängt oder an
    /// dem sie hängen. Was lokal sonst offen ist, bleibt stehen und offen; alles Übrige bekommt den Serverstand.
    /// </summary>
    /// <param name="Restored">Lokal gelöscht und vom Server zurückgeholt.</param>
    /// <param name="Kept">Lokal offen: bleibt stehen, behält seinen Manifest-Eintrag und geht beim nächsten Abgleich hinaus.</param>
    internal sealed record DiscardPlan(List<SyncRow> Upserts, List<SyncKey> Deletes, HashSet<SyncKey> Restored, HashSet<SyncKey> Kept,
        Dictionary<SyncKey, string> Manifest)
    {
        public static DiscardPlan Compute(IReadOnlyList<SyncRow> localRows, IReadOnlyList<SyncRow> remoteRows,
            IReadOnlyDictionary<SyncKey, string> manifest, IEnumerable<SyncKey> roots)
        {
            var local = localRows.ToDictionary(r => r.Key);
            var remote = remoteRows.ToDictionary(r => r.Key);
            var restored = Restorable(local, remote, manifest, roots);

            var kept = local.Keys.Concat(manifest.Keys)
                .Where(k => !restored.Contains(k) && IsOpen(k, local, remote, manifest))
                .ToHashSet();
            var keptIds = kept.Select(k => k.Id).ToHashSet();

            // Was eine offene Zeile verwendet, bleibt lokal stehen, auch wenn der Server es nicht mehr hat.
            var used = kept.Where(local.ContainsKey).SelectMany(k => References(local[k])).ToHashSet();
            var remoteIds = remote.Keys.Select(k => k.Id).ToHashSet();

            var upserts = remote.Values
                .Where(r => !keptIds.Contains(r.Id) && (!local.TryGetValue(r.Key, out var l) || l.Hash != r.Hash))
                .ToList();

            // Wie beim Abholen: eine Frage, die am Server nur den Typ gewechselt hat, ist kein Löschfall.
            var deletes = local.Keys
                .Where(k => !keptIds.Contains(k.Id) && !used.Contains(k.Id) && !remote.ContainsKey(k) && !remoteIds.Contains(k.Id))
                .ToList();

            var neu = remote.Values.Where(r => !keptIds.Contains(r.Id)).ToDictionary(r => r.Key, r => r.Hash);
            foreach (var (key, hash) in manifest)
            {
                var bleibtStehen = used.Contains(key.Id) && local.ContainsKey(key) && !remoteIds.Contains(key.Id);
                if (keptIds.Contains(key.Id) || bleibtStehen)
                    neu.TryAdd(key, hash);
            }

            return new DiscardPlan(upserts, deletes, restored, kept, neu);
        }

        /// <summary>Lokal seit dem letzten Abgleich verändert, angelegt oder gelöscht - und nicht ohnehin schon wie am Server.</summary>
        private static bool IsOpen(SyncKey key, Dictionary<SyncKey, SyncRow> local, Dictionary<SyncKey, SyncRow> remote,
            IReadOnlyDictionary<SyncKey, string> manifest)
        {
            var lokal = local.TryGetValue(key, out var l) ? l.Hash : null;
            var basis = manifest.TryGetValue(key, out var m) ? m : null;
            var server = remote.TryGetValue(key, out var r) ? r.Hash : null;

            return lokal != basis && lokal != server;
        }

        private static HashSet<SyncKey> Restorable(Dictionary<SyncKey, SyncRow> local, Dictionary<SyncKey, SyncRow> remote,
            IReadOnlyDictionary<SyncKey, string> manifest, IEnumerable<SyncKey> roots)
        {
            // Lokal gelöscht (im Manifest, lokal fehlend) und am Server noch da.
            var geloescht = new Dictionary<Guid, SyncRow>();
            foreach (var row in remote.Values.Where(r => !local.ContainsKey(r.Key) && manifest.ContainsKey(r.Key)))
                geloescht.TryAdd(row.Id, row);

            var zurueck = roots.Select(k => k.Id).Where(geloescht.ContainsKey).ToHashSet();

            // Was daran hängt (Schritte, Zellen, Ergebnisse), über alle Stufen.
            List<Guid> dazu;
            do
            {
                dazu = geloescht.Values
                    .Where(r => !zurueck.Contains(r.Id) && References(r).Any(zurueck.Contains))
                    .Select(r => r.Id)
                    .ToList();
                zurueck.UnionWith(dazu);
            }
            while (dazu.Count > 0);

            // Woran es hängt (Kategorie, Mitspieler) - sonst fehlte lokal der Fremdschlüssel.
            var offen = new Stack<Guid>(zurueck);
            while (offen.TryPop(out var id))
            {
                foreach (var ziel in References(geloescht[id]))
                {
                    if (geloescht.ContainsKey(ziel) && zurueck.Add(ziel))
                        offen.Push(ziel);
                }
            }

            return zurueck.Select(id => geloescht[id].Key).ToHashSet();
        }

        /// <summary>Die Kennungen, auf die eine Zeile zeigt: jeder Wert, der eine Guid ist. Kennungen sind eindeutig über alle Tabellen.</summary>
        internal static IEnumerable<Guid> References(SyncRow row)
            => row.Values.Values
                .Where(v => v.ValueKind == JsonValueKind.String && v.TryGetGuid(out _))
                .Select(v => v.GetGuid());
    }
}
