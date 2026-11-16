using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Sync;

namespace Quizzer.Client
{
    /// <summary>
    /// Die Massenlösch-Sperre (Durchsicht 2026-10-21, Fund 1, als zweite Linie): mehr als die Hälfte aller bekannten
    /// Fragen und Spiele, und mindestens <see cref="MassDeleteMinimum"/>, sollen weg. So sieht es aus, wenn der Abgleich
    /// gegen die falsche lokale Datenbank liest. Wer bewusst so viel löscht, gibt genau diese Löschung frei (V06).
    /// </summary>
    public sealed partial class ClientSyncService
    {
        public const int MassDeleteMinimum = 10;

        /// <summary>Schickt die lokalen Änderungen, die Sperre lässt genau die freigegebene Löschung durch.</summary>
        public async Task<PushResult> PushApprovedAsync(MassDeletion approved, CancellationToken ct = default)
        {
            await gate.WaitAsync(ct);
            try
            {
                return await PushCoreAsync(LoadState(), approved, ct);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>Wie <see cref="PullAsync(CancellationToken)"/>, das Zurückschicken vorher mit Freigabe.</summary>
        public Task<PullResult> PullApprovedAsync(MassDeletion approved, CancellationToken ct = default)
            => PullCoreAsync(approved, ct);

        /// <summary>
        /// Gezählt werden nur Fragen und Spiele (V06). Ein gelöschtes Spiel nimmt Zellen und Kopfzeilen mit, die allein
        /// schon die Hälfte ausmachten; Mitspieler, Kategorien und Designs kann ein Mitglied am Server gar nicht löschen
        /// und verdünnten nur den Vergleich - mit 50 fremden davon gingen 2 Spiele und 8 Fragen durch.
        /// </summary>
        private static bool IsRootType(string type)
            => SyncModel.TryGet(type, out var entry) && (SyncModel.IsQuestion(entry) || entry.RootType == typeof(Game));

        private static void GuardMassDelete(SyncPushRequest request, SyncState state, MassDeletion? approved)
        {
            var deletes = request.Deletes.Where(d => IsRootType(d.Type)).Select(d => d.Key).ToList();
            var known = state.Manifest.Keys.Count(k => IsRootType(k.Type));

            if (deletes.Count < MassDeleteMinimum || deletes.Count * 2 <= known)
                return;

            if (approved != null && approved.Covers(deletes))
                return;

            var games = deletes.Count(k => k.Type == nameof(Game));
            var gesperrt = new MassDeletion(deletes.Count - games, games, known, deletes);

            throw new SyncSafetyException(
                $"Der Abgleich würde {gesperrt.Description} am Server löschen - mehr als die Hälfte der {known} Fragen und Spiele, die dieses Konto kennt. "
                + "Es wurde nichts übertragen. War das Absicht, bestätige es bei „Jetzt abgleichen\" oder bei der nächsten Anmeldung; "
                + "sonst sieht es nach einer falschen lokalen Datenbank aus - dann bitte das Programm neu starten.",
                gesperrt);
        }
    }
}
