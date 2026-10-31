using System.Text.Json;
using KamCo.Api.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace KamCo.Api.Common;

/// <summary>
/// Converts domain exceptions into clean JSON ProblemDetails responses
/// instead of a raw 500 page.
/// </summary>
public static class ExceptionHandling
{
    public static IApplicationBuilder UseApiExceptionHandler(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
        {
            var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

            var (statusCode, title, detail) = exception switch
            {
                ApiException apiEx => (apiEx.StatusCode, "Request failed", apiEx.Message),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", "Authentication is required."),
                _ => (StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred.")
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            var problem = new
            {
                type = $"https://httpstatuses.com/{statusCode}",
                title,
                status = statusCode,
                detail
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
        }));

        return app;
    }
}
