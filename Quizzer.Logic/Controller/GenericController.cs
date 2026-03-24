using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.Logic.Context;
using System.Collections.Concurrent;

namespace Quizzer.Logic.Controller
{
    public abstract class GenericController<TEntity> : ControllerBase
        where TEntity : ModelBase, ICloneWithoutReferences<TEntity>, new()
    {
        protected readonly ConcurrentDictionary<Guid, object?> _PrivateModelCache = new();

        private DbSet<TEntity>? entitySet;

        protected GenericController()
        {
        }

        protected GenericController(ControllerBase other) : base(other)
        {
        }

        internal DataContext CurrentContext => Context ??= new DataContext();

        public DbSet<TEntity> EntitySet => entitySet ??= CurrentContext.GetDbSet<TEntity>();

        protected virtual Task<TEntity> BeforeActionAsync(TEntity entity, Actions action)
        {
            return Task.FromResult(entity);
        }

        protected virtual Task<TEntity> AfterActionAsync(TEntity entity, Actions action)
        {
            return Task.FromResult(entity);
        }

        protected virtual IQueryable<TEntity> SetQueryAttributes(IQueryable<TEntity> query, Actions action)
        {
            return query;
        }

        protected virtual IQueryable<TEntity> CreateReadQuery(Actions action)
        {
            return SetQueryAttributes(EntitySet.AsNoTracking().AsQueryable(), action);
        }

        protected virtual IQueryable<TEntity> CreateWriteQuery(Actions action)
        {
            return SetQueryAttributes(EntitySet.AsQueryable(), action);
        }

        protected virtual TEntity CloneForEf(TEntity entity, bool copyIdentity = true, bool clearRowVersion = false)
        {
            var clone = entity.CloneWithoutReferences(copyIdentity);

            if (clearRowVersion)
                clone.RowVersion = null;

            return clone;
        }

        protected virtual void ApplyConcurrencyOriginalValue(TEntity source, TEntity target)
        {
            if (source.RowVersion == null)
                return;

            var property = CurrentContext.Entry(target).Property<byte[]?>(nameof(ModelBase.RowVersion));
            property.OriginalValue = source.RowVersion.ToArray();
            property.IsModified = false;
        }

        protected virtual async Task<TEntity?> FindForWriteAsync(
            Guid id,
            Actions action,
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null)
        {
            var local = EntitySet.Local.FirstOrDefault(e => e.Id == id);
            if (local != null)
                return local;

            IQueryable<TEntity> query = CreateWriteQuery(action);

            if (include != null)
                query = include(query);

            return await query.FirstOrDefaultAsync(e => e.Id == id).ConfigureAwait(false);
        }

        public virtual async ValueTask<EntityEntry<TEntity>> InsertAsync(TEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            var working = await BeforeActionAsync(entity, Actions.Insert).ConfigureAwait(false);

            if (working.Id == Guid.Empty)
                working.Id = Guid.NewGuid();

            var prepared = CloneForEf(working, copyIdentity: true, clearRowVersion: true);

            var entry = await EntitySet.AddAsync(prepared).ConfigureAwait(false);

            await AfterActionAsync(entry.Entity, Actions.Insert).ConfigureAwait(false);

            return entry;
        }

        public virtual async ValueTask<TEntity?> GetAsync(Guid? id)
        {
            if (!id.HasValue || id.Value == Guid.Empty)
                return null;

            var entity = await CreateReadQuery(Actions.Get)
                .FirstOrDefaultAsync(e => e.Id == id.Value)
                .ConfigureAwait(false);

            if (entity == null)
                return null;

            return await AfterActionAsync(entity, Actions.Get).ConfigureAwait(false);
        }

        public virtual async Task<TEntity[]> GetAllAsync()
        {
            var entities = await CreateReadQuery(Actions.GetAll)
                .ToListAsync()
                .ConfigureAwait(false);

            var result = new List<TEntity>(entities.Count);

            foreach (var entity in entities)
            {
                result.Add(await AfterActionAsync(entity, Actions.GetAll).ConfigureAwait(false));
            }

            return result.ToArray();
        }

        public virtual async Task<TEntity> UpdateAsync(TEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            if (entity.Id == Guid.Empty)
                throw new InvalidOperationException("Update requires a valid Id.");

            var working = await BeforeActionAsync(entity, Actions.Update).ConfigureAwait(false);
            var prepared = CloneForEf(working, copyIdentity: true, clearRowVersion: false);

            var existing = await FindForWriteAsync(prepared.Id, Actions.Update).ConfigureAwait(false);

            if (existing == null)
            {
                var entry = CurrentContext.Attach(prepared);
                entry.State = EntityState.Modified;

                ApplyConcurrencyOriginalValue(working, entry.Entity);

                await AfterActionAsync(entry.Entity, Actions.Update).ConfigureAwait(false);
                return entry.Entity;
            }

            CurrentContext.Entry(existing).CurrentValues.SetValues(prepared);
            ApplyConcurrencyOriginalValue(working, existing);

            await AfterActionAsync(existing, Actions.Update).ConfigureAwait(false);
            return existing;
        }

        public virtual async Task<bool> DeleteAsync(Guid? id)
        {
            if (!id.HasValue || id.Value == Guid.Empty)
                return false;

            var entity = await FindForWriteAsync(id.Value, Actions.Delete).ConfigureAwait(false);

            if (entity == null)
                return false;

            await BeforeActionAsync(entity, Actions.Delete).ConfigureAwait(false);

            EntitySet.Remove(entity);

            await AfterActionAsync(entity, Actions.Delete).ConfigureAwait(false);

            return true;
        }

        public virtual async Task<bool> DeleteAsync(IEnumerable<Guid> ids)
        {
            var idList = ids?
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList() ?? new List<Guid>();

            if (idList.Count == 0)
                return true;

            var entities = new List<TEntity>(idList.Count);

            foreach (var id in idList)
            {
                var entity = await FindForWriteAsync(id, Actions.Delete).ConfigureAwait(false);
                if (entity == null)
                    return false;

                entities.Add(entity);
            }

            foreach (var entity in entities)
            {
                await BeforeActionAsync(entity, Actions.Delete).ConfigureAwait(false);
            }

            EntitySet.RemoveRange(entities);

            foreach (var entity in entities)
            {
                await AfterActionAsync(entity, Actions.Delete).ConfigureAwait(false);
            }

            return true;
        }

        public virtual async Task<int> SaveChangesAsync()
        {
            return await CurrentContext.SaveChangesAsync().ConfigureAwait(false);
        }

        public virtual async Task<int> CountAsync()
        {
            return await EntitySet.CountAsync().ConfigureAwait(false);
        }

        public virtual async Task<IEnumerable<TEntity>> UpsertAsync(IEnumerable<TEntity> entities)
        {
            ArgumentNullException.ThrowIfNull(entities);

            var result = new List<TEntity>();

            foreach (var entity in entities)
            {
                var upserted = await UpsertAsync(entity).ConfigureAwait(false);
                result.Add(upserted.Entity);
            }

            return result;
        }

        public virtual async Task<(TEntity Entity, bool Created)> UpsertAsync(
            TEntity entity,
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null)
        {
            ArgumentNullException.ThrowIfNull(entity);

            if (entity.Id == Guid.Empty)
            {
                var entry = await InsertAsync(entity).ConfigureAwait(false);
                return (entry.Entity, true);
            }

            var existing = await FindForWriteAsync(entity.Id, Actions.Update, include).ConfigureAwait(false);

            if (existing == null)
            {
                var workingInsert = await BeforeActionAsync(entity, Actions.Insert).ConfigureAwait(false);
                var preparedInsert = CloneForEf(workingInsert, copyIdentity: true, clearRowVersion: true);

                var entry = await EntitySet.AddAsync(preparedInsert).ConfigureAwait(false);

                await AfterActionAsync(entry.Entity, Actions.Insert).ConfigureAwait(false);

                return (entry.Entity, true);
            }

            var workingUpdate = await BeforeActionAsync(entity, Actions.Update).ConfigureAwait(false);
            var preparedUpdate = CloneForEf(workingUpdate, copyIdentity: true, clearRowVersion: false);

            CurrentContext.Entry(existing).CurrentValues.SetValues(preparedUpdate);
            ApplyConcurrencyOriginalValue(workingUpdate, existing);

            await AfterActionAsync(existing, Actions.Update).ConfigureAwait(false);

            return (existing, false);
        }
    }
}