using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace WaterlilyInventory.Web.Common.Results;

/// <summary>
/// Extension methods for mapping <see cref="OperationResult{T}"/> and <see cref="ModelStateDictionary"/>
/// to standardized <see cref="IActionResult"/> responses with consistent JSON envelopes.
/// Centralizes HTTP mapping without leaking HTTP concepts into the Service layer.
/// </summary>
public static class ApiResponseExtensions
{
    /// <summary>
    /// Maps an <see cref="OperationResult{T}"/> to an HTTP <see cref="IActionResult"/>.
    /// </summary>
    /// <typeparam name="T">Result data type.</typeparam>
    /// <param name="result">The service operation outcome.</param>
    /// <param name="controller">Calling controller instance.</param>
    /// <param name="createdAtActionName">Optional action name if returning 201 Created.</param>
    /// <param name="routeValues">Optional route values for the CreatedAtAction location header.</param>
    /// <returns>An <see cref="IActionResult"/> matching the operation outcome.</returns>
    public static IActionResult ToActionResult<T>(
        this OperationResult<T> result,
        ControllerBase controller,
        string? createdAtActionName = null,
        object? routeValues = null)
    {
        var traceId = Activity.Current?.Id ?? controller.HttpContext.TraceIdentifier;
        var response = ApiResponse<T>.FromOperationResult(result, traceId);

        if (result.IsSuccess)
        {
            if (!string.IsNullOrEmpty(createdAtActionName))
            {
                return controller.CreatedAtAction(createdAtActionName, routeValues, response);
            }

            return controller.Ok(response);
        }

        return result.ErrorCode switch
        {
            ErrorCodes.Validation => controller.BadRequest(response),
            ErrorCodes.NotFound => controller.NotFound(response),
            ErrorCodes.Conflict => controller.Conflict(response),
            ErrorCodes.PersistenceFailure => controller.StatusCode(StatusCodes.Status500InternalServerError, response),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError, response)
        };
    }

    /// <summary>
    /// Creates a 400 Bad Request <see cref="IActionResult"/> with standardized envelope
    /// from a failed <see cref="ModelStateDictionary"/>.
    /// </summary>
    public static IActionResult ToValidationFailureResult(
        this ModelStateDictionary modelState,
        ControllerBase controller)
    {
        var traceId = Activity.Current?.Id ?? controller.HttpContext.TraceIdentifier;

        var errors = modelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage)
                    .ToArray()
            );

        var response = ApiResponse<object?>.Failure(
            message: "Validation failed.",
            errorCode: ErrorCodes.Validation,
            validationErrors: errors,
            traceId: traceId);

        return controller.BadRequest(response);
    }
}
