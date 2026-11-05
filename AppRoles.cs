using SmartInventory.Domain.Constants;

namespace SmartInventoryManagementAPI.Authorization;

public static class AppRoles
{
    public const string Admin = RoleNames.Admin;
    public const string AdminOrManager = RoleNames.Admin + "," + RoleNames.Manager;
} 