using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.Domain.Entities;

namespace WaterlilyInventory.Web.Repositories.Interfaces;

/// <summary>
/// Small generic asynchronous repository interface for entities implementing <see cref="IEntity"/>.
/// Exposes only the necessary CRUD operations. All database I/O is asynchronous and cancellable.
/// Query methods materialize queries inside the repository; IQueryable is never exposed across the boundary.
/// Write methods complete their own persistence commits and return explicit <see cref="RepositoryWriteResult{T}"/> outcomes.
/// </summary>
/// <typeparam name="T">The entity type, constrained to reference types implementing <see cref="IEntity"/>.</typeparam>
public interface IGenericRepository<T> where T : class, IEntity
{
    /// <summary>
    /// Retrieves all entities asynchronously as a read-only list without tracking.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the database operation.</param>
    /// <returns>A read-only list of all entities in the database.</returns>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single entity by its primary key asynchronously without tracking.
    /// </summary>
    /// <param name="id">The integer primary key of the entity.</param>
    /// <param name="cancellationToken">Cancellation token for the database operation.</param>
    /// <returns>The entity if found; otherwise, null.</returns>
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity to the database and commits the transaction asynchronously.
    /// On success, returns <see cref="RepositoryWriteResult{T}.Created"/> with the generated identity key.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <param name="cancellationToken">Cancellation token for the database operation.</param>
    /// <returns>A repository write result indicating creation outcome.</returns>
    Task<RepositoryWriteResult<T>> CreateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity by loading authoritative database state, applying permitted changes,
    /// detecting whether changes actually occurred, and committing asynchronously.
    /// Returns <see cref="RepositoryWriteResult{T}.Unchanged"/> if submitted values match stored values,
    /// or <see cref="RepositoryWriteResult{T}.NotFoundResult"/> if the entity does not exist.
    /// </summary>
    /// <param name="id">The authoritative primary key of the entity to update.</param>
    /// <param name="applyChanges">Action to apply permitted modifications to the tracked entity.</param>
    /// <param name="cancellationToken">Cancellation token for the database operation.</param>
    /// <returns>A repository write result indicating updated, unchanged, not-found, or conflict outcome.</returns>
    Task<RepositoryWriteResult<T>> UpdateAsync(int id, Action<T> applyChanges, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity by loading authoritative database state, copying property values,
    /// detecting whether changes actually occurred, and committing asynchronously.
    /// </summary>
    /// <param name="entity">The entity with updated values. Primary key identifies the record.</param>
    /// <param name="cancellationToken">Cancellation token for the database operation.</param>
    /// <returns>A repository write result indicating updated, unchanged, not-found, or conflict outcome.</returns>
    Task<RepositoryWriteResult<T>> UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an existing entity by its primary key after verifying authoritative database state.
    /// Commits the deletion asynchronously.
    /// </summary>
    /// <param name="id">The primary key of the entity to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the database operation.</param>
    /// <returns>A repository write result indicating deleted, not-found, or conflict outcome.</returns>
    Task<RepositoryWriteResult<T>> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
