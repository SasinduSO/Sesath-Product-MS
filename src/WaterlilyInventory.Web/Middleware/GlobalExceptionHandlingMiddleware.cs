using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace WaterlilyInventory.Web.Middleware;

/// <summary>
/// Global exception handling middleware — the fallback for unexpected programming errors.
/// Registered early in Program.cs to wrap all subsequent request processing.
/// Expected Service failures are handled by the Service and mapped to HTTP in Controllers.
/// This middleware catches only unhandled exceptions that escape the application pipeline.
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — not an application error. Do not log as error.
            _logger.LogInformation("Request cancelled by client for {Method} {Path}.",
                context.Request.Method, context.Request.Path);
            // Do not write a response — the client is already gone
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}. TraceId: {TraceId}.",
                context.Request.Method, context.Request.Path, traceId);

            if (context.Response.HasStarted)
            {
                // Response already started — cannot rewrite. Log and let it go.
                _logger.LogWarning(
                    "Response already started for {Method} {Path}. Cannot write error response. TraceId: {TraceId}.",
                    context.Request.Method, context.Request.Path, traceId);
                return;
            }

            // Determine if the client expects JSON (API/AJAX) or HTML (page navigation)
            var acceptsJson = context.Request.Headers.Accept.Any(h =>
                h != null && h.Contains("application/json", StringComparison.OrdinalIgnoreCase));

            if (acceptsJson || IsAjaxRequest(context))
            {
                // JSON endpoints: return the established failure envelope — never expose stack traces or SQL
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var errorResponse = new
                {
                    isSuccess = false,
                    message = "An unexpected error occurred. Please try again later.",
                    traceId
                };

                var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await context.Response.WriteAsync(json);
            }
            else
            {
                // Page requests: redirect to the MVC error page
                context.Response.Redirect("/Home/Error");
            }
        }
    }

    private static bool IsAjaxRequest(HttpContext context)
    {
        return string.Equals(
            context.Request.Headers["X-Requested-With"],
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
    }
}
