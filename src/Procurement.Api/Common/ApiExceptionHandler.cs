using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Procurement.Api.Common;

/// <summary>
/// Centralised exception -> RFC 7807 ProblemDetails mapping (docs/kb/technical_kb.md
/// Conventions: every non-2xx response is a ProblemDetails). Typed <see cref="ApiException"/>
/// subclasses map to their declared status code; anything else is an unhandled 500 that never
/// leaks internals in its Detail field.
/// </summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            ApiException apiException => (apiException.StatusCode, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                // Detail only ever set for 4xx (client-actionable); never echo internals on a 5xx.
                Detail = statusCode < 500 ? exception.Message : null,
                Type = $"https://httpstatuses.io/{statusCode}",
            },
        });
    }
}
