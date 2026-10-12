namespace WaterlilyInventory.Web.Common.Results;

/// <summary>
/// Stable error codes used in <see cref="OperationResult{T}"/> to identify failure categories.
/// These are domain-level codes — mapping to HTTP status codes happens in Controllers, not here.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Input validation failure (e.g., blank name, negative price).</summary>
    public const string Validation = "Validation";

    /// <summary>Requested resource does not exist.</summary>
    public const string NotFound = "NotFound";

    /// <summary>Concurrent modification conflict (e.g., DbUpdateConcurrencyException).</summary>
    public const string Conflict = "Conflict";

    /// <summary>Unexpected database or persistence failure.</summary>
    public const string PersistenceFailure = "PersistenceFailure";
}

/// <summary>
/// Service-layer outcome for all CRUD and query operations.
/// Encapsulates success/failure state, data, message, error code, and field-level validation errors.
/// Does not contain HTTP status codes or MVC types — Controllers handle HTTP mapping.
/// </summary>
/// <typeparam name="T">The type of the result data on success.</typeparam>
public class OperationResult<T>
{
    /// <summary>Whether the operation completed successfully.</summary>
    public bool IsSuccess { get; }

    /// <summary>The result data on success; null on failure.</summary>
    public T? Data { get; }

    /// <summary>Human-readable outcome message.</summary>
    public string? Message { get; }

    /// <summary>
    /// Stable error code identifying the failure category.
    /// Null on success. One of <see cref="ErrorCodes"/> constants on failure.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Field-level validation errors keyed by field name.
    /// Null unless <see cref="ErrorCode"/> is <see cref="ErrorCodes.Validation"/>.
    /// </summary>
    public IDictionary<string, string[]>? ValidationErrors { get; }

    private OperationResult(bool isSuccess, T? data, string? message, string? errorCode,
        IDictionary<string, string[]>? validationErrors)
    {
        IsSuccess = isSuccess;
        Data = data;
        Message = message;
        ErrorCode = errorCode;
        ValidationErrors = validationErrors;
    }

    /// <summary>
    /// Creates a successful result with optional data and message.
    /// Reads of an empty collection are successful — not NotFound.
    /// </summary>
    public static OperationResult<T> Success(T data, string? message = null)
        => new(true, data, message, null, null);

    /// <summary>
    /// Creates a validation failure with a message and optional field-level errors.
    /// </summary>
    public static OperationResult<T> ValidationFailure(string message,
        IDictionary<string, string[]>? errors = null)
        => new(false, default, message, ErrorCodes.Validation, errors);

    /// <summary>
    /// Creates a not-found failure. A requested single product that does not exist is NotFound.
    /// </summary>
    public static OperationResult<T> NotFound(string message)
        => new(false, default, message, ErrorCodes.NotFound, null);

    /// <summary>
    /// Creates a conflict failure (e.g., concurrent modification detected).
    /// </summary>
    public static OperationResult<T> Conflict(string message)
        => new(false, default, message, ErrorCodes.Conflict, null);

    /// <summary>
    /// Creates a persistence failure for unexpected database errors.
    /// The Service logs the diagnostic exception once; this result carries only safe information.
    /// </summary>
    public static OperationResult<T> PersistenceFailure(string message)
        => new(false, default, message, ErrorCodes.PersistenceFailure, null);
}
