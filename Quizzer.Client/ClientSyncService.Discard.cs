using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Sync;

namespace Quizzer.Client
{
    /// <summary>
    /// V17 (M37 D5, umkehrbare Annahme F-M37-15): der Ausweg nach „Nein" bei der Massenlösch-Sperre. Die aufgehaltenen
    /// Löschungen werden verworfen und der Serverstand geholt; hinaus geht dabei nichts. Andere offene Änderungen bleiben
    /// lokal und offen, der nächste Abgleich schickt sie wie gewohnt. Der Hintergrund-Abgleich ruft das nie.
    /// </summary>
    public sealed partial class ClientSyncService
    {
        /// <summary>Holt den Serverstand und rechnet aus, was zurückkäme und was offen bliebe - für die Rückfrage. Ändert nichts.</summary>
        public async Task<MassDeletionDiscard> PrepareDiscardAsync(MassDeletion gesperrt, CancellationToken ct = default)
        {
            var snapshot = await GetSnapshotAsync(ct);
            var plan = await PlanDiscardAsync(gesperrt, snapshot, LoadState(), ct);

            var zurueck = plan.Restored.Where(k => IsRootType(k.Type)).ToList();
            var spiele = zurueck.Count(k => k.Type == nameof(Game));
            var hauptzeilen = new MassDeletion(zurueck.Count - spiele, spiele, gesperrt.Known, zurueck);

            return new MassDeletionDiscard(gesperrt, hauptzeilen, plan.Restored.Count - zurueck.Count, plan.Kept.Count);
        }

        /// <summary>
        /// Verwirft die aufgehaltenen Löschungen und spielt den Serverstand ein; lokal Offenes bleibt stehen. Serverstand,
        /// lokaler Stand und Manifest werden hier frisch gelesen - die Vorbereitung liefert nur die Zahlen für die Rückfrage.
        /// </summary>
        public async Task<PullResult> DiscardAsync(MassDeletionDiscard vorbereitet, CancellationToken ct = default)
        {
            await gate.WaitAsync(ct);
            try
            {
                var state = LoadState();
                var plan = await PlanDiscardAsync(vorbereitet.Deletion, await GetSnapshotAsync(ct), state, ct);

                // Wie beim Abholen: ein Dateifeld mit Pfad statt Namen wird geleert, bevor es lokal landet.
                foreach (var row in plan.Upserts)
                    SyncDateifelder.Entschaerfe(row);

                await using (var store = openStore())
                    await store.ApplyAsync(plan.Upserts, plan.Deletes, strict: false, cancellationToken: ct);

                // Abgewiesenes, das offen bleibt, bleibt abgewiesen; alles andere hat jetzt den Serverstand.
                var offen = plan.Kept.Select(k => k.Id).ToHashSet();
                foreach (var key in state.Suppressed.Keys.Where(k => !offen.Contains(k.Id)).ToList())
                {
                    state.Suppressed.Remove(key);
                    state.Rejections.Remove(key);
                }

                state.Manifest = plan.Manifest;
                state.LastPullAt = DateTimeOffset.UtcNow;
                stateStore.Save(state);

                var (downloaded, problems) = await DownloadMediaAsync(ct);
                return new PullResult(plan.Upserts.Count, plan.Deletes.Count, downloaded, problems);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<DiscardPlan> PlanDiscardAsync(MassDeletion gesperrt, SyncSnapshot snapshot, SyncState state, CancellationToken ct)
        {
            List<SyncRow> local;
            await using (var store = openStore())
                local = await store.ReadAllAsync(ct);

            return DiscardPlan.Compute(local, snapshot.Rows, state.Manifest, gesperrt.Keys);
        }

        private async Task<SyncSnapshot> GetSnapshotAsync(CancellationToken ct)
        {
            var snapshot = await api.GetSnapshotAsync(ct);
            if (snapshot.FormatVersion != SyncSnapshot.CurrentFormatVersion)
                throw new InvalidOperationException($"Der Server liefert Abgleichsformat {snapshot.FormatVersion}, dieses Programm kennt {SyncSnapshot.CurrentFormatVersion}. Bitte aktualisieren.");

            return snapshot;
        }
    }
}
