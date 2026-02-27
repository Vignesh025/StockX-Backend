using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace StockX.API.Middleware;

public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ErrorHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.StatusCode == (int)HttpStatusCode.NotFound &&
            !context.Response.HasStarted)
        {
            context.Response.ContentType = "application/json";

            var payload = JsonSerializer.Serialize(new
            {
                error = "Resource not found."
            });

            await context.Response.WriteAsync(payload);
        }
    }
}

