using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Context;
using Quizzer.Logic.Sync;
using Quizzer.Server.Auth;
using System.Text.Json;

namespace Quizzer.Server.Sync
{
    /// <summary>
    /// Spielt die Aenderungen eines Clients ein - Zeile fuer Zeile geprueft:
    /// <list type="number">
    /// <item><b>Form</b>: bekannter Typ, bekannte Felder, lesbare Werte - sonst <c>Invalid</c>.</item>
    /// <item><b>Stand</b>: die Zeile muss am Server noch so sein, wie der Client sie abgeholt hat
    /// (<see cref="SyncUpsert.BaseHash"/>) - sonst <c>Conflict</c>. Kein stilles Ueberschreiben.</item>
    /// <item><b>Recht</b>: siehe <see cref="WriteRules"/> - sonst <c>Forbidden</c>.</item>
    /// </list>
    /// Was durchkommt, geht in <b>einer</b> Transaktion hinein. Passt es nicht zusammen (etwa eine
    /// Zelle, deren neue Frage abgewiesen wurde), scheitert der ganze Push, und es aendert sich nichts.
    /// </summary>
    public sealed class PushService(ILogger<PushService> log)
    {
        public sealed class PushFailedException(string message, Exception inner) : Exception(message, inner);

        public async Task<SyncPushResponse> PushAsync(CurrentAccount account, SyncPushRequest request, CancellationToken ct)
        {
            if (request.FormatVersion != SyncSnapshot.CurrentFormatVersion)
                throw new ArgumentException($"Abgleichsformat {request.FormatVersion} wird nicht unterstützt.");

            var response = new SyncPushResponse();
            var acceptedUpserts = new List<SyncRow>();
            var acceptedDeletes = new List<SyncKey>();

            await using (var store = new SyncStore())
            {
                var rules = new WriteRules(store.Context, account, request);

                foreach (var upsert in request.Upserts)
                {
                    var row = upsert.Row;
                    var problem = store.Validate(row);

                    if (problem != null)
                    {
                        Reject(response, row.Type, row.Id, SyncRejectReason.Invalid, problem);
                        continue;
                    }

                    var serverEntity = await store.FindEntityAsync(row.Key, ct);
                    var serverHash = serverEntity == null ? null : store.ToRow(serverEntity).Hash;

                    if (IsConflict(upsert.BaseHash, serverHash, row.Hash))
                    {
                        Reject(response, row.Type, row.Id, SyncRejectReason.Conflict,
                            serverHash == null ? "Am Server inzwischen gelöscht." : "Am Server inzwischen geändert.");
                        continue;
                    }

                    var denied = await rules.CheckUpsertAsync(row, serverEntity, ct);
                    if (denied != null)
                    {
                        Reject(response, row.Type, row.Id, SyncRejectReason.Forbidden, denied);
                        continue;
                    }

                    acceptedUpserts.Add(row);
                }

                foreach (var delete in request.Deletes)
                {
                    if (!SyncModel.TryGet(delete.Type, out _))
                    {
                        Reject(response, delete.Type, delete.Id, SyncRejectReason.Invalid, $"Unbekannter Zeilentyp '{delete.Type}'.");
                        continue;
                    }

                    var serverEntity = await store.FindEntityAsync(delete.Key, ct);

                    if (serverEntity == null)
                    {
                        // Schon weg - das Ziel des Clients ist erreicht.
                        response.Deleted.Add(delete.Key);
                        continue;
                    }

                    if (store.ToRow(serverEntity).Hash != delete.BaseHash)
                    {
                        Reject(response, delete.Type, delete.Id, SyncRejectReason.Conflict, "Am Server inzwischen geändert.");
                        continue;
                    }

                    var denied = await rules.CheckDeleteAsync(delete, serverEntity, ct);
                    if (denied != null)
                    {
                        Reject(response, delete.Type, delete.Id, SyncRejectReason.Forbidden, denied);
                        continue;
                    }

                    acceptedDeletes.Add(delete.Key);
                }

                acceptedDeletes.AddRange(await rules.DependentsOfDeletedGamesAsync(acceptedDeletes, ct));
                acceptedDeletes = acceptedDeletes.Distinct().ToList();

                try
                {
                    await store.ApplyAsync(acceptedUpserts, acceptedDeletes, strict: true,
                        afterApply: (entity, row, isNew) => rules.Enforce(entity, isNew), cancellationToken: ct);
                }
                catch (DbUpdateException ex)
                {
                    log.LogWarning(ex, "Push von Konto {AccountId} passte nicht zusammen", account.AccountId);
                    throw new PushFailedException(
                        "Die Änderungen passen am Server nicht zusammen (etwa ein Verweis auf eine abgewiesene "
                        + "oder gelöschte Zeile). Es wurde nichts übernommen.", ex);
                }
            }

            await using (var reader = new SyncStore())
            {
                foreach (var row in acceptedUpserts)
                {
                    var current = await reader.FindRowAsync(row.Key, ct);

                    if (current != null)
                        response.Accepted.Add(new SyncRowHash { Type = current.Type, Id = current.Id, Hash = current.Hash });
                }
            }

            response.Deleted.AddRange(acceptedDeletes);

            log.LogInformation("Push von Konto {AccountId}: {Accepted} übernommen, {Deleted} gelöscht, {Rejected} abgewiesen",
                account.AccountId, response.Accepted.Count, response.Deleted.Count, response.Rejected.Count);

            return response;
        }

        /// <summary>
        /// Ein Konflikt liegt vor, wenn die Zeile am Server nicht mehr dem Stand entspricht, auf dem
        /// der Client aufbaut - es sei denn, sie ist zufaellig schon genau so, wie er sie haben will.
        /// </summary>
        internal static bool IsConflict(string? baseHash, string? serverHash, string newHash)
        {
            if (serverHash == newHash)
                return false;

            return baseHash != serverHash;
        }

        private static void Reject(SyncPushResponse response, string type, Guid id, SyncRejectReason reason, string message)
            => response.Rejected.Add(new SyncRejected { Type = type, Id = id, Reason = reason, Message = message });
    }

    /// <summary>
    /// Wer was schreiben darf (M9).
    /// <list type="bullet">
    /// <item><b>Spiel</b>: nur der Gamemaster - wer es angelegt hat. Ein neues Spiel bekommt den
    /// Anlegenden als Gamemaster, egal was der Client schickt.</item>
    /// <item><b>Kopfzeilen, Zellen, Mitspieler im Spiel, Ergebnisse</b>: wer das Spiel schreiben darf.</item>
    /// <item><b>Frage samt Schritten</b>: nur der Besitzer; eine besitzerlose Frage nimmt, wer sie
    /// aendert (wie <c>QuestionOwnership.ClaimIfUnowned</c> lokal).</item>
    /// <item><b>Mitspieler</b>: neue Gaeste darf jeder anlegen; aendern darf man sich selbst und
    /// Gaeste ohne Konto.</item>
    /// <item><b>Kategorien, Designs</b>: jeder anlegen und aendern, loeschen nur der Admin.</item>
    /// </list>
    /// Der Admin darf alles.
    /// </summary>
    internal sealed class WriteRules(DataContext context, CurrentAccount account, SyncPushRequest request)
    {
        private readonly Dictionary<Guid, bool> gameCache = new();
        private readonly Dictionary<Guid, bool> questionCache = new();
        private readonly Dictionary<Guid, (Guid? Owner, bool Exists)> questionOwners = new();
        private readonly Dictionary<Guid, Guid?> gameMasters = new();

        private readonly HashSet<Guid> newGamesInPush = request.Upserts
            .Where(u => u.Row.Type == nameof(Game) && u.BaseHash == null)
            .Select(u => u.Row.Id).ToHashSet();

        private readonly HashSet<Guid> newQuestionsInPush = request.Upserts
            .Where(u => u.BaseHash == null && SyncModel.TryGet(u.Row.Type, out var e) && SyncModel.IsQuestion(e))
            .Select(u => u.Row.Id).ToHashSet();

        private Guid Me => account.PlayerId;

        public async Task<string?> CheckUpsertAsync(SyncRow row, object? serverEntity, CancellationToken ct)
        {
            var denied = await CheckUpsertCoreAsync(row, serverEntity, ct);

            // Der Admin darf alles - aber erst nach der Pruefung, denn sie merkt sich den
            // bisherigen Besitzer und Gamemaster, die Enforce bewahren muss.
            return account.IsAdmin ? null : denied;
        }

        private async Task<string?> CheckUpsertCoreAsync(SyncRow row, object? serverEntity, CancellationToken ct)
        {
            switch (serverEntity ?? row.Type)
            {
                case Category or GameTheme:
                case string and (nameof(Category) or nameof(GameTheme)):
                    return null;

                case Player player:
                    return await CanWritePlayerAsync(player.Id, ct) ? null : "Fremde Mitspieler mit Konto lassen sich nicht ändern.";

                case string and nameof(Player):
                    return null;

                case QuestionBase question:
                    return await CanWriteQuestionAsync(question.Id, ct) ? null : "Die Frage gehört jemand anderem.";

                case string type when SyncModel.IsQuestion(SyncModel.Get(type)):
                    return null;

                case QuestionStepResource step:
                    return await CanWriteQuestionAsync(step.QuestionBaseId, ct)
                        && await CanWriteQuestionAsync(GetGuid(row, nameof(QuestionStepResource.QuestionBaseId)), ct)
                        ? null : "Die Frage gehört jemand anderem.";

                case string and nameof(QuestionStepResource):
                    return await CanWriteQuestionAsync(GetGuid(row, nameof(QuestionStepResource.QuestionBaseId)), ct)
                        ? null : "Die Frage gehört jemand anderem.";

                case Game game:
                    return await CanWriteGameAsync(game.Id, ct) ? null : "Nur der Gamemaster darf das Spiel ändern.";

                case string and nameof(Game):
                    return null;

                default:
                    // Kopfzeile, Zelle, Spielteilnahme, Ergebnis: am bisherigen und am neuen Spiel.
                    var newGame = GetGuid(row, "GameId");
                    var oldGame = serverEntity == null ? newGame : (Guid)context.Entry(serverEntity).Property("GameId").CurrentValue!;

                    if (!(await CanWriteGameAsync(newGame, ct) && await CanWriteGameAsync(oldGame, ct)))
                        return "Nur der Gamemaster darf das Spiel ändern.";

                    // Zelle und Ergebnis verweisen auf eine Frage - und ein Verweis macht sie fuer
                    // jeden im Spiel sichtbar (Visibility.QuestionIds). Verweisen darf man deshalb
                    // nur auf Fragen, die man selbst sehen darf; ein unveraenderter Verweis bleibt
                    // erlaubt (Durchsicht 2026-10-21, Fund 7).
                    if (row.Values.ContainsKey("QuestionBaseId") && GetNullableGuid(row, "QuestionBaseId") is { } questionId)
                    {
                        var before = serverEntity == null ? null : context.Entry(serverEntity).Property("QuestionBaseId").CurrentValue as Guid?;
                        if (before != questionId && !await CanReferenceQuestionAsync(questionId, ct))
                            return "Auf diese Frage darf das Spiel nicht verweisen.";
                    }

                    return null;
            }
        }

        public async Task<string?> CheckDeleteAsync(SyncDelete delete, object serverEntity, CancellationToken ct)
        {
            if (account.IsAdmin)
                return null;

            switch (serverEntity)
            {
                case Category or GameTheme or Player:
                    return "Löschen darf hier nur der Admin.";

                case QuestionBase question:
                    if (!await CanWriteQuestionAsync(question.Id, ct))
                        return "Die Frage gehört jemand anderem.";

                    // Ergebnisse haengen mit Cascade an der Frage: das Loeschen naehme fremde
                    // Spielhistorie mit. Das darf nur, wem alle betroffenen Spiele gehoeren.
                    var games = await context.GameGridCoordinates.Where(c => c.QuestionBaseId == question.Id).Select(c => c.GameId)
                        .Union(context.QuestionResults.Where(r => r.QuestionBaseId == question.Id).Select(r => r.GameId))
                        .Distinct().ToListAsync(ct);

                    foreach (var game in games)
                    {
                        if (!await CanWriteGameAsync(game, ct))
                            return "Die Frage steckt in einem fremden Spiel.";
                    }

                    return null;

                case QuestionStepResource step:
                    return await CanWriteQuestionAsync(step.QuestionBaseId, ct) ? null : "Die Frage gehört jemand anderem.";

                case Game game:
                    return await CanWriteGameAsync(game.Id, ct) ? null : "Nur der Gamemaster darf das Spiel löschen.";

                default:
                    var gameId = (Guid)context.Entry(serverEntity).Property("GameId").CurrentValue!;
                    return await CanWriteGameAsync(gameId, ct) ? null : "Nur der Gamemaster darf das Spiel ändern.";
            }
        }

        /// <summary>
        /// Ein geloeschtes Spiel nimmt alles mit, was an ihm haengt - die Fremdschluessel stehen
        /// auf NO ACTION, also muss der Server es selbst tun (wie <c>GamesController</c> lokal).
        /// </summary>
        public async Task<List<SyncKey>> DependentsOfDeletedGamesAsync(IReadOnlyCollection<SyncKey> deletes, CancellationToken ct)
        {
            var gameIds = deletes.Where(d => d.Type == nameof(Game)).Select(d => d.Id).ToList();

            if (gameIds.Count == 0)
                return [];

            var keys = new List<SyncKey>();

            keys.AddRange((await context.QuestionResults.Where(r => gameIds.Contains(r.GameId)).Select(r => r.Id).ToListAsync(ct))
                .Select(id => new SyncKey(nameof(QuestionResult), id)));
            keys.AddRange((await context.PlayerXGames.Where(x => gameIds.Contains(x.GameId)).Select(x => x.Id).ToListAsync(ct))
                .Select(id => new SyncKey(nameof(PlayerXGame), id)));
            keys.AddRange((await context.GameGridCoordinates.Where(c => gameIds.Contains(c.GameId)).Select(c => c.Id).ToListAsync(ct))
                .Select(id => new SyncKey(nameof(GameGridCoordinate), id)));
            keys.AddRange((await context.Headers.Where(h => gameIds.Contains(h.GameId)).Select(h => h.Id).ToListAsync(ct))
                .Select(id => new SyncKey(nameof(Header), id)));

            return keys;
        }

        /// <summary>
        /// Felder, die der Client nicht bestimmen darf: der Besitzer einer Frage und der
        /// Gamemaster eines Spiels (standards-sicherheit A01, Feldrechte).
        /// </summary>
        public void Enforce(object entity, bool isNew)
        {
            switch (entity)
            {
                case QuestionBase question:
                    {
                        var previous = questionOwners.TryGetValue(question.Id, out var known) && known.Exists ? known.Owner : null;
                        question.OwnerPlayerId = previous ?? Me;
                        break;
                    }

                case Game game:
                    {
                        var previous = gameMasters.TryGetValue(game.Id, out var known) ? known : null;
                        game.ModeratorPlayerId = previous ?? Me;
                        break;
                    }
            }
        }

        private async Task<bool> CanWriteGameAsync(Guid gameId, CancellationToken ct)
        {
            // Auch fuer den Admin wird nachgesehen: Enforce braucht den bisherigen Gamemaster.
            // Frueher kehrte der Admin hier sofort zurueck - und wurde bei jeder Korrektur an
            // einem fremden Spiel selbst dessen Gamemaster (Durchsicht 2026-10-21, Fund 2).
            if (gameCache.TryGetValue(gameId, out var known))
                return known;

            var master = await context.Games.AsNoTracking().Where(g => g.Id == gameId)
                .Select(g => new { g.ModeratorPlayerId }).FirstOrDefaultAsync(ct);

            bool allowed;
            if (master == null)
            {
                allowed = newGamesInPush.Contains(gameId);
            }
            else
            {
                gameMasters[gameId] = master.ModeratorPlayerId;
                allowed = account.IsAdmin || master.ModeratorPlayerId == null || master.ModeratorPlayerId == Me;
            }

            if (account.IsAdmin)
                allowed = true;

            gameCache[gameId] = allowed;
            return allowed;
        }

        private async Task<bool> CanWriteQuestionAsync(Guid questionId, CancellationToken ct)
        {
            // Wie bei den Spielen: auch der Admin braucht den bisherigen Besitzer fuer Enforce.
            if (questionCache.TryGetValue(questionId, out var known))
                return known;

            var owner = await context.QuestionBases.AsNoTracking().Where(q => q.Id == questionId)
                .Select(q => new { q.OwnerPlayerId }).FirstOrDefaultAsync(ct);

            bool allowed;
            if (owner == null)
            {
                questionOwners[questionId] = (null, false);
                allowed = newQuestionsInPush.Contains(questionId);
            }
            else
            {
                questionOwners[questionId] = (owner.OwnerPlayerId, true);
                allowed = owner.OwnerPlayerId == null || owner.OwnerPlayerId == Me;
            }

            if (account.IsAdmin)
                allowed = true;

            questionCache[questionId] = allowed;
            return allowed;
        }

        private async Task<bool> CanWritePlayerAsync(Guid playerId, CancellationToken ct)
        {
            if (account.IsAdmin || playerId == Me)
                return true;

            return !await context.Accounts.AnyAsync(a => a.PlayerId == playerId, ct);
        }

        private async Task<bool> CanReferenceQuestionAsync(Guid questionId, CancellationToken ct)
        {
            if (account.IsAdmin || newQuestionsInPush.Contains(questionId))
                return true;

            return await context.QuestionBases.AsNoTracking().AnyAsync(q => q.Id == questionId
                && (q.OwnerPlayerId == null || q.OwnerPlayerId == Me || q.IsShared), ct);
        }

        private static Guid? GetNullableGuid(SyncRow row, string name)
            => row.Values.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetGuid()
                : null;

        private static Guid GetGuid(SyncRow row, string name)
            => row.Values.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetGuid()
                : Guid.Empty;
    }
}
