using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Exceptions;
using ValidationException = SmartInventory.Application.Exceptions.ValidationException;

namespace SmartInventoryManagementAPI.Middleware;

public class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger,
    IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                ValidationException => (400, "Validation error"),
                UnauthorizedException => (401, "Unauthorized"),
                NotFoundException => (404, "Not found"),
                ConcurrencyException => (409, "Conflict"),
                _ => (500, "Server error")
            };

            if (status == 500) logger.LogError(ex, "Unhandled exception");

            // 500 par asli error sirf Development mein dikhta hai, production mein generic message
            var detail = status == 500
                ? (env.IsDevelopment() ? $"{ex.GetType().Name}: {ex.Message}" : "An unexpected error occurred.")
                : ex.Message;

            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            });
        }
    }
}