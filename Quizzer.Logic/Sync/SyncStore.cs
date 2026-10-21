using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Context;
using Quizzer.Logic.Controller.TypedControllers;
using System.Text.Json;

namespace Quizzer.Logic.Sync
{
    /// <summary>
    /// Liest Zeilen aus einer Datenbank und spielt Zeilen in eine Datenbank ein. Dieselbe Klasse
    /// arbeitet am Server (Push einspielen) und im WPF (Snapshot einspielen) - es gibt nur
    /// <b>eine</b> Stelle, die weiss, wie aus einer Zeile wieder eine Entitaet wird.
    /// </summary>
    public sealed class SyncStore : IDisposable, IAsyncDisposable
    {
        private readonly DataContext context;
        private readonly string? connectionString;

        /// <summary>Gegen <c>Settings.ConnectionString</c> - am Server und im WPF.</summary>
        public SyncStore()
        {
            context = new DataContext();
        }

        /// <summary>Gegen eine bestimmte Datenbank (Tests, in denen Server und Client im selben Prozess laufen).</summary>
        public SyncStore(string connectionString)
        {
            this.connectionString = connectionString;
            context = new DataContext(connectionString);
        }

        internal DataContext Context => context;

        /// <summary>Alle Zeilen aller Abgleich-Typen - die lokale Sicht des Clients.</summary>
        public async Task<List<SyncRow>> ReadAllAsync(CancellationToken cancellationToken = default)
        {
            var rows = new List<SyncRow>();

            await AddAsync(rows, context.Categories, cancellationToken);
            await AddAsync(rows, context.GameThemes, cancellationToken);
            await AddAsync(rows, context.Players, cancellationToken);
            await AddAsync(rows, context.QuestionBases, cancellationToken);
            await AddAsync(rows, context.QuestionStepResources, cancellationToken);
            await AddAsync(rows, context.Games, cancellationToken);
            await AddAsync(rows, context.Headers, cancellationToken);
            await AddAsync(rows, context.GameGridCoordinates, cancellationToken);
            await AddAsync(rows, context.PlayerXGames, cancellationToken);
            await AddAsync(rows, context.QuestionResults, cancellationToken);

            return rows;
        }

        internal async Task AddAsync<T>(List<SyncRow> rows, IQueryable<T> query, CancellationToken cancellationToken)
            where T : class
        {
            var entities = await query.AsNoTracking().ToListAsync(cancellationToken);
            rows.AddRange(entities.Select(e => SyncRowConverter.ToRow(context.Model, e)));
        }

        public SyncRow ToRow(object entity) => SyncRowConverter.ToRow(context.Model, entity);

        /// <summary>
        /// Prueft eine Zeile, ohne sie einzuspielen: bekannter Typ, nur bekannte Felder, jeder Wert
        /// lesbar. Liefert die Beanstandung oder <c>null</c>.
        /// </summary>
        public string? Validate(SyncRow row)
        {
            if (!SyncModel.TryGet(row.Type, out var entry))
                return $"Unbekannter Zeilentyp '{row.Type}'.";

            if (row.Id == Guid.Empty)
                return "Eine Zeile ohne Id.";

            var properties = SyncRowConverter.SyncedProperties(context.Model, entry.ClrType)
                .ToDictionary(p => p.Name, StringComparer.Ordinal);

            foreach (var (name, element) in row.Values)
            {
                if (!properties.TryGetValue(name, out var property))
                    return $"'{row.Type}' hat kein Feld '{name}'.";

                try
                {
                    var value = element.Deserialize(property.ClrType, SyncRowConverter.JsonOptions);

                    if (value == null && !property.IsNullable)
                        return $"'{row.Type}.{name}' darf nicht leer sein.";
                }
                catch (System.Text.Json.JsonException)
                {
                    return $"'{row.Type}.{name}' hat einen ungültigen Wert.";
                }
            }

            return null;
        }

        /// <summary>Die Entitaet zu einem Schluessel (verfolgt), oder <c>null</c>.</summary>
        public async Task<object?> FindEntityAsync(SyncKey key, CancellationToken cancellationToken = default)
        {
            var entry = SyncModel.Get(key.Type);
            return await context.FindAsync(entry.RootType, [key.Id], cancellationToken);
        }

        /// <summary>Die aktuelle Zeile zu einem Schluessel, oder <c>null</c>.</summary>
        public async Task<SyncRow?> FindRowAsync(SyncKey key, CancellationToken cancellationToken = default)
        {
            var entry = SyncModel.Get(key.Type);
            var entity = await context.FindAsync(entry.RootType, [key.Id], cancellationToken);

            if (entity == null)
                return null;

            // Die Zeile gehoert zum Schluessel nur, wenn auch der Typ passt - eine Frage, die am
            // Server inzwischen ein anderer Fragetyp ist, ist eine andere Zeile.
            return SyncRowConverter.ToRow(context.Model, entity);
        }

        /// <summary>
        /// Spielt Loeschungen und Zeilen ein, in einer Transaktion.
        /// <list type="number">
        /// <item>Fragen, deren Typ sich geaendert hat, werden zuerst umgewandelt - ueber
        /// <see cref="QuestionBasesController.ConvertTypeAsync"/>, weil Loeschen und Neuanlegen die
        /// Spielhistorie mitnaehme.</item>
        /// <item>Loeschungen laufen <b>vor</b> den Zeilen und in umgekehrter Rangfolge, damit ein
        /// geloeschter und neu angelegter Eintrag mit gleichem eindeutigen Index nicht kollidiert.</item>
        /// <item>Zeilen laufen in Rangfolge; EF ordnet die Einfuegungen nach den Fremdschluesseln.</item>
        /// </list>
        /// <paramref name="afterApply"/> darf Felder nach dem Einspielen ueberschreiben (der
        /// Server setzt dort zum Beispiel den Besitzer).
        /// </summary>
        public async Task ApplyAsync(
            IReadOnlyList<SyncRow> upserts,
            IReadOnlyList<SyncKey> deletes,
            bool strict,
            Action<object, SyncRow, bool>? afterApply = null,
            CancellationToken cancellationToken = default)
        {
            await ConvertChangedQuestionTypesAsync(upserts, cancellationToken);

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var orderedDeletes = deletes
                .Select(d => (Key: d, Entry: SyncModel.Get(d.Type)))
                .OrderByDescending(d => d.Entry.Rank)
                .ToList();

            foreach (var (key, entry) in orderedDeletes)
            {
                var entity = await context.FindAsync(entry.RootType, [key.Id], cancellationToken);

                if (entity != null)
                    context.Remove(entity);
            }

            await context.SaveChangesAsync(cancellationToken);

            foreach (var row in upserts.OrderBy(r => SyncModel.Get(r.Type).Rank))
            {
                var entry = SyncModel.Get(row.Type);
                var entity = await context.FindAsync(entry.RootType, [row.Id], cancellationToken);
                var isNew = entity == null;

                if (entity == null)
                {
                    entity = Activator.CreateInstance(entry.ClrType, nonPublic: true)
                        ?? throw new SyncFormatException($"'{row.Type}' lässt sich nicht anlegen.");

                    var newEntry = context.Entry(entity);
                    newEntry.Property("Id").CurrentValue = row.Id;
                    SyncRowConverter.ApplyValues(newEntry, row, strict);
                    newEntry.State = EntityState.Added;
                }
                else
                {
                    if (entity.GetType() != entry.ClrType)
                        throw new SyncFormatException($"'{row.Id}' ist hier ein {entity.GetType().Name}, kein {row.Type}.");

                    SyncRowConverter.ApplyValues(context.Entry(entity), row, strict);
                }

                afterApply?.Invoke(entity, row, isNew);
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        private async Task ConvertChangedQuestionTypesAsync(IReadOnlyList<SyncRow> upserts, CancellationToken cancellationToken)
        {
            var questionRows = upserts
                .Where(r => SyncModel.TryGet(r.Type, out var e) && SyncModel.IsQuestion(e))
                .ToList();

            if (questionRows.Count == 0)
                return;

            var ids = questionRows.Select(r => r.Id).ToList();
            var existing = await context.QuestionBases.AsNoTracking()
                .Where(q => ids.Contains(q.Id))
                .ToListAsync(cancellationToken);

            foreach (var question in existing)
            {
                var row = questionRows.First(r => r.Id == question.Id);

                if (question.GetType().Name == row.Type)
                    continue;

                var target = (QuestionBase)Activator.CreateInstance(SyncModel.Get(row.Type).ClrType, nonPublic: true)!;

                using var controller = new QuestionBasesController();

                if (connectionString != null)
                {
                    // Der Controller oeffnet sonst einen Kontext auf Settings.ConnectionString -
                    // also womoeglich auf eine andere Datenbank als dieser Abgleich.
                    controller.Context.Dispose();
                    controller.Context = new DataContext(connectionString);
                }

                await controller.ConvertTypeAsync(question.Id, target.Typ);
            }

            context.ChangeTracker.Clear();
        }

        public void Dispose() => context.Dispose();

        public ValueTask DisposeAsync() => context.DisposeAsync();
    }
}
