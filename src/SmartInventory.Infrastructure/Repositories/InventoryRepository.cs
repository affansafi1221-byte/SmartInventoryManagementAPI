using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Entities;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Repositories;

public class InventoryRepository(AppDbContext db) : Repository<Inventory>(db), IInventoryRepository
{
    public Task<Inventory?> GetByProductIdAsync(int productId, CancellationToken ct = default)
        => Set.Include(i => i.Product)
              .FirstOrDefaultAsync(i => i.ProductId == productId, ct);

    public Task<List<Inventory>> GetAllWithProductAsync(CancellationToken ct = default)
        => Set.AsNoTracking()
              .Include(i => i.Product)
              .Where(i => i.Product.IsActive)
              .OrderBy(i => i.Product.Name)
              .ToListAsync(ct);
}

public class StockMovementRepository(AppDbContext db) : Repository<StockMovement>(db), IStockMovementRepository
{
    public Task<List<StockMovement>> GetHistoryAsync(int? productId, int take, CancellationToken ct = default)
        => Set.AsNoTracking()
              .Include(m => m.Product)
              .Where(m => productId == null || m.ProductId == productId)
              .OrderByDescending(m => m.CreatedAt)
              .Take(take)
              .ToListAsync(ct);
}