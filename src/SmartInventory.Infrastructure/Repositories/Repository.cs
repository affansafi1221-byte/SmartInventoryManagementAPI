using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Common;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Repositories;

public class Repository<T>(AppDbContext db) : IRepository<T> where T : BaseEntity
{
    protected readonly DbSet<T> Set = db.Set<T>();

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<T>> GetAllAsync(CancellationToken ct = default)
        => Set.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await Set.AddAsync(entity, ct);

    public void Update(T entity) => Set.Update(entity);
    public void Remove(T entity) => Set.Remove(entity);
}