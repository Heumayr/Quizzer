using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.DataModels.Transfer.Sync;
using System.Text.Json;

namespace Quizzer.Server.Sync
{
    /// <summary>Die Aufstellung eines Spiels als Ganzes (M37 P5, R1-7) - eigene Teildatei wie <c>WriteRules.Team.cs</c>.</summary>
    internal sealed partial class WriteRules
    {
        /// <summary>
        /// Die Teams jedes Spiels halten nach dem Push <see cref="TeamGrenzen.AufstellungProblem"/> ein - wie
        /// <c>SaveLineupAsync</c> und der Bündel-Import. Geprüft wird das Ganze, nicht die Zeile: zwei Teams tauschen ihre Farben
        /// in einem Push. Sonst gehen die Teamzeilen dieses Spiels nicht durch (<c>Invalid</c>, auch beim Admin); Löschungen
        /// bleiben - sie machen eine Aufstellung nie schlechter.
        /// </summary>
        public async Task CheckLineupsAsync(List<SyncRow> acceptedUpserts, IReadOnlyCollection<SyncKey> acceptedDeletes, SyncPushResponse response, CancellationToken ct)
        {
            var zeilen = acceptedUpserts.Where(r => r.Type == nameof(GameTeam)).ToList();

            if (zeilen.Count == 0)
                return;

            var ids = zeilen.Select(r => r.Id).ToList();
            var bisher = await context.GameTeams.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
            var geloescht = acceptedDeletes.Where(d => d.Type == nameof(GameTeam)).Select(d => d.Id).ToHashSet();

            // Ohne GameId in der Zeile bleibt das Spiel des Servers (V15: ein Team wechselt es nie).
            foreach (var spiel in zeilen.GroupBy(r => GetNullableGuid(r, nameof(GameTeam.GameId)) ?? bisher.GetValueOrDefault(r.Id)?.GameId ?? Guid.Empty))
            {
                var amServer = await context.GameTeams.AsNoTracking().Where(t => t.GameId == spiel.Key).ToListAsync(ct);
                var aufstellung = amServer.Where(t => !geloescht.Contains(t.Id) && spiel.All(r => r.Id != t.Id))
                    .Concat(spiel.Select(r => NachDemPush(r, bisher.GetValueOrDefault(r.Id))))
                    .ToList();

                if (TeamGrenzen.AufstellungProblem(aufstellung) is not { } problem)
                    continue;

                foreach (var zeile in spiel)
                {
                    acceptedUpserts.Remove(zeile);
                    response.Rejected.Add(new SyncRejected { Type = zeile.Type, Id = zeile.Id, Reason = SyncRejectReason.Invalid, Message = problem });
                }
            }
        }

        /// <summary>Das Team, wie es nach dem Einspielen dasteht: was die Zeile bringt, sonst der Stand am Server.</summary>
        private static GameTeam NachDemPush(SyncRow row, GameTeam? bisher) => new()
        {
            Id = row.Id,
            Name = row.Values.TryGetValue(nameof(GameTeam.Name), out var name) && name.ValueKind == JsonValueKind.String
                ? name.GetString() ?? string.Empty
                : bisher?.Name ?? string.Empty,
            ColorIndex = IntOf(row, nameof(GameTeam.ColorIndex)) ?? bisher?.ColorIndex ?? 0,
            SpeakerTurn = IntOf(row, nameof(GameTeam.SpeakerTurn)) ?? bisher?.SpeakerTurn ?? 0,
        };

        private static int? IntOf(SyncRow row, string name)
            => row.Values.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var zahl) ? zahl : null;
    }
}
