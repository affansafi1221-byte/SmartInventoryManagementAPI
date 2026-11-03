using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Exceptions;
using System.ComponentModel.DataAnnotations;
using ValidationException = SmartInventory.Application.Exceptions.ValidationException;

namespace SmartInventoryManagementAPI.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                ValidationException => (400, "Validation error"),
                NotFoundException => (404, "Not found"),
                _ => (500, "Server error")
            };

            if (status == 500) logger.LogError(ex, "Unhandled exception");

            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == 500 ? "An unexpected error occurred." : ex.Message
            });

            



        }
    }
}