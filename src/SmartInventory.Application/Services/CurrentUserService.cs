using Microsoft.AspNetCore.Http;
using SmartInventory.Application.Interfaces;

namespace SmartInventoryManagementAPI.Services;

public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    public int? UserId =>
        int.TryParse(accessor.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;

    public string? Role => accessor.HttpContext?.User.FindFirst("role")?.Value;
}