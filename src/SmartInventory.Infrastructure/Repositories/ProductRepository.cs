using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Entities;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Repositories;

public class ProductRepository(AppDbContext db) : Repository<Product>(db), IProductRepository
{
    public Task<List<Product>> GetAllWithDetailsAsync(CancellationToken ct = default)
        => Set.AsNoTracking()
              .Include(p => p.Category)
              .Include(p => p.Inventory)
              .Where(p => p.IsActive)
              .OrderBy(p => p.Name)
              .ToListAsync(ct);

    public Task<Product?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default)
        => Set.Include(p => p.Category)
              .Include(p => p.Inventory)
              .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default)
        => Set.AnyAsync(p => p.Sku == sku.ToUpper(), ct);
}