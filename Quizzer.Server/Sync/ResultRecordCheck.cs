using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Context;
using System.Text.Json;

namespace Quizzer.Server.Sync
{
    /// <summary>
    /// Plausibilitaet der Bestenliste am Ergebnis (M35 C9, A06/A08): was der Client als Rohwert und Klaus schickt, ist
    /// Vorschlag. Eine Zeile ausserhalb des Bereichs ihrer Art ist <c>Invalid</c>, der Rest des Pushes geht durch - wie
    /// die Laengengrenze am Joker. Der Gamemaster bleibt die Vertrauensinstanz fuer Werte im Bereich.
    /// </summary>
    internal static class ResultRecordCheck
    {
        /// <summary>Die Beanstandung oder <c>null</c>; Zeilen anderer Typen gehen unbesehen durch.</summary>
        public static async Task<string?> CheckAsync(SyncRow row, DataContext context, SyncPushRequest request, CancellationToken ct)
        {
            if (row.Type != nameof(QuestionResult))
                return null;

            if (IntOf(row, nameof(QuestionResult.Steals)) is < 0)
                return "Die Zahl der Klaus darf nicht negativ sein.";

            if (IntOf(row, nameof(QuestionResult.RecordValue)) is not { } wert)
                return null;

            var frage = GuidOf(row, nameof(QuestionResult.QuestionBaseId));
            var art = frage == null ? null : await KindOfAsync(frage.Value, context, request, ct);

            if (art == null || !MinigameProfiles.TryFor(art.Value, out var profil) || profil.Record is not { } record)
                return "Ein Rohwert an einer Frage, deren Art keine Bestenliste führt.";

            return record.IsPlausible(wert)
                ? null
                : $"Der Rohwert {wert} liegt außerhalb des Bereichs der Art „{profil.DisplayName}“ ({record.Min} bis {record.Max}).";
        }

        /// <summary>Die Art der Frage - aus demselben Push, sonst vom Server; <c>null</c>, wenn sie kein Minispiel ist.</summary>
        private static async Task<MinigameKind?> KindOfAsync(Guid frage, DataContext context, SyncPushRequest request, CancellationToken ct)
        {
            if (request.Upserts.FirstOrDefault(u => u.Row.Id == frage && u.Row.Type != nameof(QuestionResult))?.Row is { } neu)
                return neu.Type == nameof(MinigameQuestion) && IntOf(neu, nameof(MinigameQuestion.Kind)) is { } kind ? (MinigameKind)kind : null;

            return await context.MinigameQuestions.AsNoTracking()
                .Where(q => q.Id == frage)
                .Select(q => (MinigameKind?)q.Kind)
                .FirstOrDefaultAsync(ct);
        }

        private static int? IntOf(SyncRow row, string name)
            => row.Values.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var zahl)
                ? zahl
                : null;

        private static Guid? GuidOf(SyncRow row, string name)
            => row.Values.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var id)
                ? id
                : null;
    }
}
