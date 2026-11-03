using SmartInventory.Domain.Entities;

namespace SmartInventory.Application.Interfaces;

public interface IProductRepository : IRepository<Product>
{
    Task<List<Product>> GetAllWithDetailsAsync(CancellationToken ct = default);
    Task<Product?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
    Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default);
    Task<(List<Product> Items, int Total)> GetPagedAsync(
        string? search, int? categoryId, int page, int pageSize, CancellationToken ct = default);
}