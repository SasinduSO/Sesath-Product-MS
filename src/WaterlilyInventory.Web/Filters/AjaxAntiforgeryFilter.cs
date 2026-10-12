using System.Diagnostics;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using WaterlilyInventory.Web.Common.Results;

namespace WaterlilyInventory.Web.Filters;

/// <summary>
/// Global authorization filter that validates antiforgery tokens on all state-mutating requests (POST, PUT, DELETE, PATCH).
/// If validation fails, it catches the <see cref="AntiforgeryValidationException"/> and returns
/// a standardized JSON error envelope with HTTP 400 Bad Request, preventing HTML error pages from
/// breaking AJAX callers or corrupting client-side table rendering.
/// </summary>
public class AjaxAntiforgeryFilter : IAsyncAuthorizationFilter
{
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<AjaxAntiforgeryFilter> _logger;

    public AjaxAntiforgeryFilter(IAntiforgery antiforgery, ILogger<AjaxAntiforgeryFilter> logger)
    {
        _antiforgery = antiforgery ?? throw new ArgumentNullException(nameof(antiforgery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;

        // Skip safe HTTP methods (GET, HEAD, OPTIONS, TRACE)
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return;
        }

        // Honor explicit [IgnoreAntiforgeryToken] metadata if present
        if (context.ActionDescriptor.EndpointMetadata.Any(em => em is IgnoreAntiforgeryTokenAttribute))
        {
            return;
        }

        try
        {
            await _antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException ex)
        {
            _logger.LogWarning(ex, "Antiforgery token validation failed for {Method} {Path}.",
                method, context.HttpContext.Request.Path);

            var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

            var response = ApiResponse<object?>.Failure(
                message: "Antiforgery validation failed. Please refresh the page and try again.",
                errorCode: "AntiforgeryValidationFailed",
                validationErrors: null,
                traceId: traceId);

            context.Result = new ObjectResult(response)
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }
    }
}
