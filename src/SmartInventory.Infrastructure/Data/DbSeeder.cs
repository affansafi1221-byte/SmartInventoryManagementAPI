using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Constants;
using SmartInventory.Domain.Entities;

namespace SmartInventory.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAdminAsync(IServiceProvider services, IConfiguration config)
    {
        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        email = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email)) return;

        db.Users.Add(new User
        {
            FullName = "System Admin",
            Email = email,
            PasswordHash = hasher.Hash(password),
            RoleId = RoleIds.Admin
        });
        await db.SaveChangesAsync();
    }
}