using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Entities;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Repositories;

public class UserRepository(AppDbContext db) : Repository<User>(db), IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => Set.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdWithRoleAsync(int id, CancellationToken ct = default)
        => Set.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => Set.AnyAsync(u => u.Email == email, ct);
}