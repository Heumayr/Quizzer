using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Quizzer.DataModels.Transfer.Sync;
using System.Text.Json;

namespace Quizzer.Logic.Sync
{
    /// <summary>Verweise auf gelöschte Zeilen am Server (M37 P7, P5-2) - eigene Teildatei, wie die Teams in <c>ReleaseTeamReferencesAsync</c>.</summary>
    public sealed partial class SyncStore
    {
        /// <summary>
        /// EF setzt beim Löschen den Verweis jedes verfolgten Kindes selbst auf null (NO ACTION, ClientSetNull) - und der Server
        /// verfolgt jede Zeile des Pushs, auch eine abgewiesene. So verlor eine Zelle still ihre Frage. Frei wird deshalb nur, was
        /// derselbe Push umhängt (wie R1-1), ausdrücklich und auch nach geleertem Tracker; jedes andere verfolgte Kind wird
        /// losgelassen, damit das Löschen an der Datenbank scheitert statt still.
        /// </summary>
        private async Task ReleaseRepointedReferencesAsync(IReadOnlyList<SyncRow> upserts, IReadOnlyList<SyncKey> deletes, CancellationToken cancellationToken)
        {
            var geloescht = deletes.Select(d => d.Id).ToHashSet();

            if (geloescht.Count == 0)
                return;

            var umgehaengt = RepointedFields(upserts, geloescht);

            foreach (var row in upserts.Where(r => umgehaengt.ContainsKey(r.Id)))
                await context.FindAsync(SyncModel.Get(row.Type).RootType, [row.Id], cancellationToken);

            foreach (var entry in context.ChangeTracker.Entries().ToList())
            {
                if (entry.Metadata.FindProperty("Id") == null || entry.Property("Id").CurrentValue is not Guid id || geloescht.Contains(id))
                    continue;

                foreach (var property in ClientNulledForeignKeys(entry.Metadata))
                {
                    if (entry.Property(property.Name).CurrentValue is not Guid ziel || !geloescht.Contains(ziel))
                        continue;

                    if (property.IsNullable && umgehaengt.TryGetValue(id, out var felder) && felder.Contains(property.Name))
                    {
                        entry.Property(property.Name).CurrentValue = null;
                        continue;
                    }

                    entry.State = EntityState.Detached;
                    break;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Je Zeile die Verweisfelder, die sie mitbringt und nicht auf eine gelöschte Zeile setzt.</summary>
        private Dictionary<Guid, HashSet<string>> RepointedFields(IReadOnlyList<SyncRow> upserts, HashSet<Guid> geloescht)
        {
            var result = new Dictionary<Guid, HashSet<string>>();

            foreach (var row in upserts)
            {
                if (context.Model.FindEntityType(SyncModel.Get(row.Type).ClrType) is not { } type)
                    continue;

                var felder = ClientNulledForeignKeys(type)
                    .Where(p => p.IsNullable && row.Values.TryGetValue(p.Name, out var wert) && !(Ziel(wert) is Guid neu && geloescht.Contains(neu)))
                    .Select(p => p.Name)
                    .ToHashSet();

                if (felder.Count > 0)
                    result[row.Id] = felder;
            }

            return result;
        }

        /// <summary>Die Verweise, die EF bei einem gelöschten Elternteil selbst anfasst - CASCADE und SET NULL tut die Datenbank.</summary>
        private static IEnumerable<IProperty> ClientNulledForeignKeys(IEntityType type)
            => type.GetForeignKeys()
                .Where(fk => fk.Properties.Count == 1 && fk.DeleteBehavior is DeleteBehavior.ClientSetNull or DeleteBehavior.NoAction)
                .Select(fk => fk.Properties[0]);

        private static Guid? Ziel(JsonElement wert)
            => wert.ValueKind == JsonValueKind.String && wert.TryGetGuid(out var id) ? id : null;
    }
}
