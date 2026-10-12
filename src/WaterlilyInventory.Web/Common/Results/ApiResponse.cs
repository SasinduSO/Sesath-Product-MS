namespace WaterlilyInventory.Web.Common.Results;

/// <summary>
/// Uniform JSON envelope returned by all API/AJAX endpoints.
/// Uses camelCase property naming consistent with the frontend client expectations.
/// </summary>
/// <typeparam name="T">The type of payload data.</typeparam>
public class ApiResponse<T>
{
    /// <summary>Whether the operation completed successfully.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Payload data on success; null on failure.</summary>
    public T? Data { get; set; }

    /// <summary>Human-readable outcome or error message.</summary>
    public string? Message { get; set; }

    /// <summary>Stable error code identifying the failure category (e.g. Validation, NotFound, Conflict).</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Field-level validation error messages, if any.</summary>
    public IDictionary<string, string[]>? ValidationErrors { get; set; }

    /// <summary>Correlation / trace identifier for diagnostics.</summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// Creates an <see cref="ApiResponse{T}"/> from an <see cref="OperationResult{T}"/>.
    /// </summary>
    public static ApiResponse<T> FromOperationResult(OperationResult<T> result, string? traceId = null)
    {
        return new ApiResponse<T>
        {
            IsSuccess = result.IsSuccess,
            Data = result.Data,
            Message = result.Message,
            ErrorCode = result.ErrorCode,
            ValidationErrors = result.ValidationErrors,
            TraceId = traceId
        };
    }

    /// <summary>
    /// Creates a successful response envelope.
    /// </summary>
    public static ApiResponse<T> Success(T data, string? message = null, string? traceId = null)
    {
        return new ApiResponse<T>
        {
            IsSuccess = true,
            Data = data,
            Message = message,
            TraceId = traceId
        };
    }

    /// <summary>
    /// Creates a failure response envelope.
    /// </summary>
    public static ApiResponse<T> Failure(string message, string errorCode, IDictionary<string, string[]>? validationErrors = null, string? traceId = null)
    {
        return new ApiResponse<T>
        {
            IsSuccess = false,
            Data = default,
            Message = message,
            ErrorCode = errorCode,
            ValidationErrors = validationErrors,
            TraceId = traceId
        };
    }
}
