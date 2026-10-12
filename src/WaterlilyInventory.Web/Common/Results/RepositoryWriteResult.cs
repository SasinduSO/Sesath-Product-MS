namespace WaterlilyInventory.Web.Common.Results;

/// <summary>
/// Explicit outcomes for repository write operations.
/// Services inspect this to determine what happened at the database level.
/// </summary>
public enum WriteOutcome
{
    /// <summary>A new entity was successfully created and committed.</summary>
    Created,

    /// <summary>An existing entity was modified and committed.</summary>
    Updated,

    /// <summary>The submitted values matched the stored values — no database write was needed.</summary>
    Unchanged,

    /// <summary>The entity was successfully deleted and committed.</summary>
    Deleted,

    /// <summary>The entity was not found in the database.</summary>
    NotFound,

    /// <summary>A concurrent modification conflict was detected (e.g., DbUpdateConcurrencyException).</summary>
    Conflict
}

/// <summary>
/// Repository-layer write result with explicit outcomes.
/// Returned by repository create, update, and delete operations.
/// Unexpected persistence errors throw <see cref="Common.Exceptions.PersistenceException"/>
/// rather than using this type — this type represents known, expected outcomes.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public class RepositoryWriteResult<T> where T : class
{
    /// <summary>The outcome of the write operation.</summary>
    public WriteOutcome Outcome { get; }

    /// <summary>The entity after the operation, if applicable. Null for NotFound.</summary>
    public T? Entity { get; }

    private RepositoryWriteResult(WriteOutcome outcome, T? entity)
    {
        Outcome = outcome;
        Entity = entity;
    }

    /// <summary>Entity was successfully created and committed. Contains the persisted entity with generated Id.</summary>
    public static RepositoryWriteResult<T> Created(T entity) => new(WriteOutcome.Created, entity);

    /// <summary>Entity was successfully updated and committed.</summary>
    public static RepositoryWriteResult<T> Updated(T entity) => new(WriteOutcome.Updated, entity);

    /// <summary>Submitted values matched stored values — no actual change was needed.</summary>
    public static RepositoryWriteResult<T> Unchanged(T entity) => new(WriteOutcome.Unchanged, entity);

    /// <summary>Entity was successfully deleted and committed.</summary>
    public static RepositoryWriteResult<T> Deleted(T entity) => new(WriteOutcome.Deleted, entity);

    /// <summary>Entity was not found in the database.</summary>
    public static RepositoryWriteResult<T> NotFoundResult() => new(WriteOutcome.NotFound, null);

    /// <summary>A concurrent modification conflict was detected.</summary>
    public static RepositoryWriteResult<T> ConflictResult() => new(WriteOutcome.Conflict, null);
}
