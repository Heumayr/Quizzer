using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Options;
using Quizzer.DataModels.Models;
using Quizzer.Logic.Context;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Quizzer.DataModels.Enumerations;

namespace Quizzer.Logic.Controller
{
    /// <summary>
    /// Implements the default behavior of a controller for a specific entity.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    public abstract class GenericController<TEntity> : ControllerBase
        where TEntity : ModelBase, new()
    {
        private DbSet<TEntity>? entitySet;

        /// <summary>
        /// Creates a new data context.
        /// </summary>
        protected GenericController()
        {
        }

        /// <summary>
        /// Uses the data context and authentication token of another controller.
        /// </summary>
        /// <param name="other">The other controller to copy the context and session token from.</param>
        protected GenericController(ControllerBase other) : base(other)
        {
        }

        /// <summary>
        /// Selects the data set by the generic type.
        /// </summary>
        internal DbSet<TEntity> EntitySet
        {
            get
            {
                if (entitySet == null)
                {
                    if (Context != null)
                        entitySet = Context.GetDbSet<TEntity>();
                    else
                    {
                        using var tempContext = new DataContext();

                        entitySet = tempContext.GetDbSet<TEntity>();
                    }
                }

                return entitySet;
            }
        }

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

        /// <summary>
        /// Adds an entity to the data set.
        /// </summary>
        /// <param name="entity">The entity to insert.</param>
        /// <returns>The entity entry of the inserted entity.</returns>
        internal virtual async ValueTask<EntityEntry<TEntity>> InsertAsync(TEntity entity)
        {
            return await EntitySet.AddAsync(await BeforeActionAsync(entity, Actions.Insert)).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets an entity from the database by id.
        /// </summary>
        /// <param name="id">The id of the searched entity.</param>
        /// <returns>The entity if found, otherwise null.</returns>
        internal virtual async ValueTask<TEntity?> GetAsync(Guid? id)
        {
            var query = EntitySet.AsQueryable();

            query = SetQueryAttributes(query, Actions.Get);

            var entity = await query.FirstOrDefaultAsync(e => e.Id == id).ConfigureAwait(false);

            if (entity != null)
                _ = await AfterActionAsync(entity, Actions.Get).ConfigureAwait(false);

            return entity;
        }

        /// <summary>
        /// Gets all entries in the database. Must be restricted.
        /// </summary>
        /// <returns>An array with all entries.</returns>
        internal virtual async Task<TEntity[]> GetAllAsync()
        {
            var query = EntitySet.AsNoTracking().AsQueryable();

            query = SetQueryAttributes(query, Actions.GetAll);

            var entities = await query.ToListAsync().ConfigureAwait(false);

            foreach (var entity in entities)
            {
                _ = await AfterActionAsync(entity, Actions.GetAll).ConfigureAwait(false);
            }

            return entities.ToArray();
        }

        /// <summary>
        /// Updates a specific entity.
        /// </summary>
        /// <param name="entity">The updated entity.</param>
        /// <returns>The updated entity.</returns>
        internal virtual Task<TEntity> UpdateAsync(TEntity entity)
        {
            return Task.Run(async () =>
            {
                return EntitySet.Update(await BeforeActionAsync(entity, Actions.Update).ConfigureAwait(false)).Entity;
            });
        }

        /// <summary>
        /// Removes an entry from the database by id.
        /// </summary>
        /// <param name="id">The id of the entity to remove.</param>
        /// <returns>True if removing was successful, otherwise false.</returns>
        internal virtual async Task<bool> DeleteAsync(Guid? id)
        {
            var entity = await GetAsync(id).ConfigureAwait(false);

            if (entity == null)
                return false;

            await BeforeActionAsync(entity, Actions.Delete).ConfigureAwait(false);

            //delete
            EntitySet.Remove(entity);

            //soft delete
            //entity.Deleted = DateTime.Now;
            //EntitySet.Update(entity);

            await AfterActionAsync(entity, Actions.Delete).ConfigureAwait(false);

            return true;
        }

        /// <summary>
        /// Removes multiple entries from the database by their ids.
        /// </summary>
        /// <param name="ids">The ids of the entities to remove.</param>
        /// <returns>True if all removals were successful, otherwise false.</returns>
        internal virtual async Task<bool> DeleteAsync(IEnumerable<Guid> ids)
        {
            foreach (var id in ids)
            {
                if (!await DeleteAsync(id).ConfigureAwait(false))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Saves all changes in the current data context.
        /// </summary>
        /// <returns>The number of state entries written to the database.</returns>
        internal virtual async Task<int> SaveChangesAsync()
        {
            if (Context == null)
                return 0;

            var result = await Context.SaveChangesAsync().ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Gets the number of entries in the database.
        /// </summary>
        /// <returns>The number of entries.</returns>
        internal virtual async Task<int> CountAsync()
        {
            return await EntitySet.CountAsync().ConfigureAwait(false);
        }
    }
}