using SmartInventory.Domain.Entities;

namespace SmartInventory.Application.Interfaces;

public interface IInventoryRepository : IRepository<Inventory>
{
    Task<Inventory?> GetByProductIdAsync(int productId, CancellationToken ct = default);
    Task<List<Inventory>> GetAllWithProductAsync(CancellationToken ct = default);
}

public interface IStockMovementRepository : IRepository<StockMovement>
{
    Task<List<StockMovement>> GetHistoryAsync(int? productId, int take, CancellationToken ct = default);
}