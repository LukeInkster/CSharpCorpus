// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Internal;
#if NET451
using System.ComponentModel;
using Microsoft.EntityFrameworkCore.Internal;

#endif
namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    ///     <para>
    ///         A <see cref="DbSet{TEntity}" /> can be used to query and save instances of <typeparamref name="TEntity" />.
    ///         LINQ queries against a <see cref="DbSet{TEntity}" /> will be translated into queries against the database.
    ///     </para>
    ///     <para>
    ///         The results of a LINQ query against a <see cref="DbSet{TEntity}" /> will contain the results
    ///         returned from the database and may not reflect changes made in the context that have not
    ///         been persisted to the database. For example, the results will not contain newly added entities
    ///         and may still contain entities that are marked for deletion.
    ///     </para>
    ///     <para>
    ///         Depending on the database being used, some parts of a LINQ query against a <see cref="DbSet{TEntity}" />
    ///         may be evaluated in memory rather than being translated into a database query.
    ///     </para>
    ///     <para>
    ///         <see cref="DbSet{TEntity}" /> objects are usually obtained from a <see cref="DbSet{TEntity}" />
    ///         property on a derived <see cref="DbContext" /> or from the <see cref="DbContext.Set{TEntity}" />
    ///         method.
    ///     </para>
    /// </summary>
    /// <typeparam name="TEntity"> The type of entity being operated on by this set. </typeparam>
    public abstract partial class DbSet<TEntity>
        : IQueryable<TEntity>, IAsyncEnumerableAccessor<TEntity>, IInfrastructure<IServiceProvider>
        where TEntity : class
    {
        /// <summary>
        ///     Finds an entity with the given primary key values. If an entity with the given primary key values
        ///     is being tracked by the context, then it is returned immediately without making a request to the
        ///     database. Otherwise, a query is made to the dataabse for an entity with the given primary key values
        ///     and this entity, if found, is attached to the context and returned. If no entity is found, then
        ///     null is returned.
        /// </summary>
        /// <param name="keyValues">The values of the primary key for the entity to be found.</param>
        /// <returns>The entity found, or null.</returns>
        public virtual TEntity Find([NotNull] params object[] keyValues)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Finds an entity with the given primary key values. If an entity with the given primary key values
        ///     is being tracked by the context, then it is returned immediately without making a request to the
        ///     database. Otherwise, a query is made to the dataabse for an entity with the given primary key values
        ///     and this entity, if found, is attached to the context and returned. If no entity is found, then
        ///     null is returned.
        /// </summary>
        /// <param name="keyValues">The values of the primary key for the entity to be found.</param>
        /// <returns>The entity found, or null.</returns>
        public virtual Task<TEntity> FindAsync([NotNull] params object[] keyValues)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Finds an entity with the given primary key values. If an entity with the given primary key values
        ///     is being tracked by the context, then it is returned immediately without making a request to the
        ///     database. Otherwise, a query is made to the dataabse for an entity with the given primary key values
        ///     and this entity, if found, is attached to the context and returned. If no entity is found, then
        ///     null is returned.
        /// </summary>
        /// <param name="keyValues">The values of the primary key for the entity to be found.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
        /// <returns>The entity found, or null.</returns>
        public virtual Task<TEntity> FindAsync([NotNull] object[] keyValues, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Begins tracking the given entity, and any other reachable entities that are
        ///     not already being tracked, in the <see cref="EntityState.Added" /> state such that they will
        ///     be inserted into the database when <see cref="DbContext.SaveChanges()" /> is called.
        /// </summary>
        /// <param name="entity"> The entity to add. </param>
        /// <returns>
        ///     The <see cref="EntityEntry{TEntity}" /> for the entity. The entry provides
        ///     access to change tracking information and operations for the entity.
        /// </returns>
        public virtual EntityEntry<TEntity> Add([NotNull] TEntity entity)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     <para>
        ///         Begins tracking the given entity, and any other reachable entities that are
        ///         not already being tracked, in the <see cref="EntityState.Added" /> state such that they will
        ///         be inserted into the database when <see cref="DbContext.SaveChanges()" /> is called.
        ///     </para>
        ///     <para>
        ///         This method is async only to allow special value generators, such as the one used by
        ///         'Microsoft.EntityFrameworkCore.Metadata.SqlServerValueGenerationStrategy.SequenceHiLo',
        ///         to access the database asynchronously. For all other cases the non async method should be used.
        ///     </para>
        /// </summary>
        /// <param name="entity"> The entity to add. </param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
        /// <returns>
        ///     A task that represents the asynchronous Add operation. The task result contains the
        ///     <see cref="EntityEntry{TEntity}" /> for the entity. The entry provides access to change tracking
        ///     information and operations for the entity.
        /// </returns>
        public virtual Task<EntityEntry<TEntity>> AddAsync(
            [NotNull] TEntity entity,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Begins tracking the given entity, and any other reachable entities that are
        ///     not already being tracked, in the <see cref="EntityState.Unchanged" /> state such that no
        ///     operation will be performed when <see cref="DbContext.SaveChanges()" /> is called.
        /// </summary>
        /// <param name="entity"> The entity to attach. </param>
        /// <returns>
        ///     The <see cref="EntityEntry" /> for the entity. The entry provides
        ///     access to change tracking information and operations for the entity.
        /// </returns>
        public virtual EntityEntry<TEntity> Attach([NotNull] TEntity entity)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Begins tracking the given entity in the <see cref="EntityState.Deleted" /> state such that it will
        ///     be removed from the database when <see cref="DbContext.SaveChanges()" /> is called.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         If the entity is already tracked in the <see cref="EntityState.Added" /> state then the context will
        ///         stop tracking the entity (rather than marking it as <see cref="EntityState.Deleted" />) since the
        ///         entity was previously added to the context and does not exist in the database.
        ///     </para>
        ///     <para>
        ///         Any other reachable entities that are not already being tracked will be tracked in the same way that
        ///         they would be if <see cref="Attach(TEntity)" /> was called before calling this method.
        ///         This allows any cascading actions to be applied when <see cref="DbContext.SaveChanges()" /> is called.
        ///     </para>
        /// </remarks>
        /// <param name="entity"> The entity to remove. </param>
        /// <returns>
        ///     The <see cref="EntityEntry{TEntity}" /> for the entity. The entry provides
        ///     access to change tracking information and operations for the entity.
        /// </returns>
        public virtual EntityEntry<TEntity> Remove([NotNull] TEntity entity)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     <para>
        ///         Begins tracking the given entity, and any other reachable entities that are
        ///         not already being tracked, in the <see cref="EntityState.Modified" /> state such that it will
        ///         be updated in the database when <see cref="DbContext.SaveChanges()" /> is called.
        ///     </para>
        ///     <para>
        ///         All properties of the entity will be marked as modified. To mark only some properties as modified, use
        ///         <see cref="Attach(TEntity)" /> to begin tracking the entity in the <see cref="EntityState.Unchanged" />
        ///         state and then use the returned <see cref="EntityEntry" /> to mark the desired properties as modified.
        ///     </para>
        /// </summary>
        /// <param name="entity"> The entity to update. </param>
        /// <returns>
        ///     The <see cref="EntityEntry" /> for the entity. The entry provides
        ///     access to change tracking information and operations for the entity.
        /// </returns>
        public virtual EntityEntry<TEntity> Update([NotNull] TEntity entity)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Begins tracking the given entities, and any other reachable entities that are
        ///     not already being tracked, in the <see cref="EntityState.Added" /> state such that they will
        ///     be inserted into the database when <see cref="DbContext.SaveChanges()" /> is called.
        /// </summary>
        /// <param name="entities"> The entities to add. </param>
        public virtual void AddRange([NotNull] params TEntity[] entities)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     <para>
        ///         Begins tracking the given entities, and any other reachable entities that are
        ///         not already being tracked, in the <see cref="EntityState.Added" /> state such that they will
        ///         be inserted into the database when <see cref="DbContext.SaveChanges()" /> is called.
        ///     </para>
        ///     <para>
        ///         This method is async only to allow special value generators, such as the one used by
        ///         'Microsoft.EntityFrameworkCore.Metadata.SqlServerValueGenerationStrategy.SequenceHiLo',
        ///         to access the database asynchronously. For all other cases the non async method should be used.
        ///     </para>
        /// </summary>
        /// <param name="entities"> The entities to add. </param>
        /// <returns> A task that represents the asynchronous operation. </returns>
        public virtual Task AddRangeAsync([NotNull] params TEntity[] entities)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Begins tracking the given entities, and any other reachable entities that are
        ///     not already being tracked, in the <see cref="EntityState.Unchanged" /> state such that no
        ///     operation will be performed when <see cref="DbContext.SaveChanges()" /> is called.
        /// </summary>
        /// <param name="entities"> The entities to attach. </param>
        public virtual void AttachRange([NotNull] params TEntity[] entities)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Begins tracking the given entity in the <see cref="EntityState.Deleted" /> state such that it will
        ///     be removed from the database when <see cref="DbContext.SaveChanges()" /> is called.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         If any of the entities are already tracked in the <see cref="EntityState.Added" /> state then the context will
        ///         stop tracking those entities (rather than marking them as <see cref="EntityState.Deleted" />) since those
        ///         entities were previously added to the context and do not exist in the database.
        ///     </para>
        ///     <para>
        ///         Any other reachable entities that are not already being tracked will be tracked in the same way that
        ///         they would be if <see cref="AttachRange(TEntity[])" /> was called before calling this method.
        ///         This allows any cascading actions to be applied when <see cref="DbContext.SaveChanges()" /> is called.
        ///     </para>
        /// </remarks>
        /// <param name="entities"> The entities to remove. </param>
        public virtual void RemoveRange([NotNull] params TEntity[] entities)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     <para>
        ///         Begins tracking the given entities, and any other reachable entities that are
        ///         not already being tracked, in the <see cref="EntityState.Modified" /> state such that they will
        ///         be updated in the database when <see cref="DbContext.SaveChanges()" /> is called.
        ///     </para>
        ///     <para>
        ///         All properties of the entities will be marked as modified. To mark only some properties as modified, use
        ///         <see cref="Attach(TEntity)" /> to begin tracking each entity in the <see cref="EntityState.Unchanged" />
        ///         state and then use the returned <see cref="EntityEntry" /> to mark the desired properties as modified.
        ///     </para>
        /// </summary>
        /// <param name="entities"> The entities to update. </param>
        public virtual void UpdateRange([NotNull] params TEntity[] entities)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Begins tracking the given entities, and any other reachable entities that are
        ///     not already being tracked, in the <see cref="EntityState.Added" /> state such that they will
        ///     be inserted into the database when <see cref="DbContext.SaveChanges()" /> is called.
        /// </summary>
        /// <param name="entities"> The entities to add. </param>
        public virtual void AddRange([NotNull] IEnumerable<TEntity> entities)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     <para>
        ///         Begins tracking the given entities, and any other reachable entities that are
        ///         not already being tracked, in the <see cref="EntityState.Added" /> state such that they will
        ///         be inserted into the database when <see cref="DbContext.SaveChanges()" /> is called.
        ///     </para>
        ///     <para>
        ///         This method is async only to allow special value generators, such as the one used by
        ///         'Microsoft.EntityFrameworkCore.Metadata.SqlServerValueGenerationStrategy.SequenceHiLo',
        ///         to access the database asynchronously. For all other cases the non async method should be used.
        ///     </para>
        /// </summary>
        /// <param name="entities"> The entities to add. </param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
        /// <returns> A task that represents the asynchronous operation. </returns>
        public virtual Task AddRangeAsync(
            [NotNull] IEnumerable<TEntity> entities,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Begins tracking the given entities, and any other reachable entities that are
        ///     not already being tracked, in the <see cref="EntityState.Unchanged" /> state such that no
        ///     operation will be performed when <see cref="DbContext.SaveChanges()" /> is called.
        /// </summary>
        /// <param name="entities"> The entities to attach. </param>
        public virtual void AttachRange([NotNull] IEnumerable<TEntity> entities)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Begins tracking the given entity in the <see cref="EntityState.Deleted" /> state such that it will
        ///     be removed from the database when <see cref="DbContext.SaveChanges()" /> is called.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         If any of the entities are already tracked in the <see cref="EntityState.Added" /> state then the context will
        ///         stop tracking those entities (rather than marking them as <see cref="EntityState.Deleted" />) since those
        ///         entities were previously added to the context and do not exist in the database.
        ///     </para>
        ///     <para>
        ///         Any other reachable entities that are not already being tracked will be tracked in the same way that
        ///         they would be if <see cref="AttachRange(IEnumerable{TEntity})" /> was called before calling this method.
        ///         This allows any cascading actions to be applied when <see cref="DbContext.SaveChanges()" /> is called.
        ///     </para>
        /// </remarks>
        /// <param name="entities"> The entities to remove. </param>
        public virtual void RemoveRange([NotNull] IEnumerable<TEntity> entities)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     <para>
        ///         Begins tracking the given entities, and any other reachable entities that are
        ///         not already being tracked, in the <see cref="EntityState.Modified" /> state such that they will
        ///         be updated in the database when <see cref="DbContext.SaveChanges()" /> is called.
        ///     </para>
        ///     <para>
        ///         All properties of the entities will be marked as modified. To mark only some properties as modified, use
        ///         <see cref="Attach(TEntity)" /> to begin tracking each entity in the <see cref="EntityState.Unchanged" />
        ///         state and then use the returned <see cref="EntityEntry" /> to mark the desired properties as modified.
        ///     </para>
        /// </summary>
        /// <param name="entities"> The entities to update. </param>
        public virtual void UpdateRange([NotNull] IEnumerable<TEntity> entities)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Returns an <see cref="IEnumerator{T}" /> which when enumerated will execute a query against the database
        ///     to load all entities from the database.
        /// </summary>
        /// <returns> The query results. </returns>
        IEnumerator<TEntity> IEnumerable<TEntity>.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Returns an <see cref="IEnumerator" /> which when enumerated will execute a query against the database
        ///     to load all entities from the database.
        /// </summary>
        /// <returns> The query results. </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Returns an <see cref="IAsyncEnumerable{T}" /> which when enumerated will asynchronously execute the query against
        ///     the database.
        /// </summary>
        /// <returns> The query results. </returns>
        IAsyncEnumerable<TEntity> IAsyncEnumerableAccessor<TEntity>.AsyncEnumerable
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        ///     Gets the IQueryable element type.
        /// </summary>
        Type IQueryable.ElementType
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        ///     Gets the IQueryable LINQ Expression.
        /// </summary>
        Expression IQueryable.Expression
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        ///     Gets the IQueryable provider.
        /// </summary>
        IQueryProvider IQueryable.Provider
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        ///     <para>
        ///         Gets the scoped <see cref="IServiceProvider" /> being used to resolve services.
        ///     </para>
        ///     <para>
        ///         This property is intended for use by extension methods that need to make use of services
        ///         not directly exposed in the public API surface.
        ///     </para>
        /// </summary>
        IServiceProvider IInfrastructure<IServiceProvider>.Instance
        {
            get { throw new NotImplementedException(); }
        }
    }

#if NET451

    public abstract partial class DbSet<TEntity> : IListSource
        where TEntity : class
    {
        /// <summary>
        ///     <para>
        ///         This method is called by data binding frameworks when attempting to data bind directly to a <see cref="DbSet{TEntity}" />.
        ///     </para>
        ///     <para>
        ///         This implementation always throws an exception as binding directly to a <see cref="DbSet{TEntity}" /> will result in a query being
        ///         sent to the database every time the data binding framework requests the contents of the collection. Instead materialize the results
        ///         into a collection, by calling a method such as <see cref="Enumerable.ToList{TSource}(IEnumerable{TSource})" /> or
        ///         <see cref="Enumerable.ToArray{TSource}(IEnumerable{TSource})" />, and bind to the collection.
        ///     </para>
        /// </summary>
        /// <exception cref="NotSupportedException"> Always thrown. </exception>
        /// <returns> Never returns, always throws an exception. </returns>
        IList IListSource.GetList()
        {
            throw new NotSupportedException(CoreStrings.DataBindingWithIListSource);
        }

        /// <summary>
        ///     Gets a value indicating whether the collection is a collection of System.Collections.IList objects.
        ///     Always returns false.
        /// </summary>
        bool IListSource.ContainsListCollection => false;
    }
#endif
}
