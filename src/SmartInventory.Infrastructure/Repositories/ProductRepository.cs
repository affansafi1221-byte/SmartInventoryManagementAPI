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

    public async Task<(List<Product> Items, int Total)> GetPagedAsync(
        string? search, int? categoryId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = Set.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Inventory)
            .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p => p.Name.Contains(s) || p.Sku.Contains(s));
        }

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
} 