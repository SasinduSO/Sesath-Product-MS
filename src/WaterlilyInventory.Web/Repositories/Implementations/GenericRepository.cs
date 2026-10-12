using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WaterlilyInventory.Web.Common.Exceptions;
using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.Data;
using WaterlilyInventory.Web.Domain.Entities;
using WaterlilyInventory.Web.Repositories.Interfaces;

namespace WaterlilyInventory.Web.Repositories.Implementations;

/// <summary>
/// Generic repository implementation using Entity Framework Core.
/// Materializes queries before returning to enforce repository boundaries.
/// Write methods complete their own persistence commits and return explicit <see cref="RepositoryWriteResult{T}"/> outcomes.
/// </summary>
/// <typeparam name="T">The entity type implementing <see cref="IEntity"/>.</typeparam>
public class GenericRepository<T> : IGenericRepository<T> where T : class, IEntity
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;
    protected readonly ILogger<GenericRepository<T>> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="GenericRepository{T}"/>.
    /// </summary>
    /// <param name="context">The scoped EF Core database context.</param>
    /// <param name="logger">Logger for repository diagnostic messages.</param>
    public GenericRepository(ApplicationDbContext context, ILogger<GenericRepository<T>> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = context.Set<T>();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // AsNoTracking avoids change-tracking overhead for read-only queries
            var entities = await _dbSet.AsNoTracking().ToListAsync(cancellationToken);
            return entities.AsReadOnly();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve all entities of type {EntityType}.", typeof(T).Name);
            throw new PersistenceException($"Database read error while retrieving all {typeof(T).Name} records.", ex);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve entity of type {EntityType} with Id {EntityId}.", typeof(T).Name, id);
            throw new PersistenceException($"Database read error while retrieving {typeof(T).Name} with ID {id}.", ex);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<RepositoryWriteResult<T>> CreateAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        try
        {
            // Synchronous tracking operation — does not perform I/O
            _dbSet.Add(entity);

            // Asynchronous commit performs the actual database I/O
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Created entity {EntityType} with generated Id {EntityId}.", typeof(T).Name, entity.Id);
            return RepositoryWriteResult<T>.Created(entity);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database update error creating entity of type {EntityType}.", typeof(T).Name);
            throw new PersistenceException($"Failed to persist new {typeof(T).Name} to the database.", ex);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<RepositoryWriteResult<T>> UpdateAsync(
        int id,
        Action<T> applyChanges,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applyChanges);

        try
        {
            // Load authoritative database state; never rely on cached existence
            var existing = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
            if (existing is null)
            {
                _logger.LogDebug("Update target {EntityType} with Id {EntityId} was not found.", typeof(T).Name, id);
                return RepositoryWriteResult<T>.NotFoundResult();
            }

            // Apply submitted permitted changes to the tracked entity
            applyChanges(existing);

            // Guard: ensure the primary key was not modified
            _context.Entry(existing).Property(e => e.Id).IsModified = false;

            // Detect whether anything actually changed in the EF change tracker
            var entry = _context.Entry(existing);
            if (entry.State == EntityState.Unchanged)
            {
                _logger.LogDebug("No changes detected for {EntityType} with Id {EntityId}.", typeof(T).Name, id);
                return RepositoryWriteResult<T>.Unchanged(existing);
            }

            // Asynchronous commit performs the actual database I/O
            var rowsAffected = await _context.SaveChangesAsync(cancellationToken);
            if (rowsAffected > 0)
            {
                _logger.LogDebug("Updated {EntityType} with Id {EntityId}.", typeof(T).Name, id);
                return RepositoryWriteResult<T>.Updated(existing);
            }

            // If tracker had modifications but 0 rows were affected, treat as conflict
            _logger.LogWarning("Update for {EntityType} with Id {EntityId} affected 0 rows.", typeof(T).Name, id);
            return RepositoryWriteResult<T>.ConflictResult();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict detected while updating {EntityType} with Id {EntityId}.", typeof(T).Name, id);
            return RepositoryWriteResult<T>.ConflictResult();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database update error updating {EntityType} with Id {EntityId}.", typeof(T).Name, id);
            throw new PersistenceException($"Failed to update {typeof(T).Name} with ID {id}.", ex);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<RepositoryWriteResult<T>> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return await UpdateAsync(
            entity.Id,
            existing => _context.Entry(existing).CurrentValues.SetValues(entity),
            cancellationToken);
    }

    /// <inheritdoc/>
    public virtual async Task<RepositoryWriteResult<T>> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            // Load authoritative database state; never rely on cached existence
            var existing = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
            if (existing is null)
            {
                _logger.LogDebug("Delete target {EntityType} with Id {EntityId} was not found.", typeof(T).Name, id);
                return RepositoryWriteResult<T>.NotFoundResult();
            }

            // Synchronous tracking operation
            _dbSet.Remove(existing);

            // Asynchronous commit performs the actual database I/O
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Deleted {EntityType} with Id {EntityId}.", typeof(T).Name, id);
            return RepositoryWriteResult<T>.Deleted(existing);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict detected while deleting {EntityType} with Id {EntityId}.", typeof(T).Name, id);
            return RepositoryWriteResult<T>.ConflictResult();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error deleting {EntityType} with Id {EntityId}.", typeof(T).Name, id);
            throw new PersistenceException($"Failed to delete {typeof(T).Name} with ID {id}.", ex);
        }
    }
}
