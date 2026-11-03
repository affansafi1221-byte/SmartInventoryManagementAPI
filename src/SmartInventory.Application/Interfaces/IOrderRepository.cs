using SmartInventory.Domain.Entities;
using SmartInventory.Domain.Enums;

namespace SmartInventory.Application.Interfaces;

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
    Task<List<Order>> GetAllWithDetailsAsync(OrderStatus? status, CancellationToken ct = default);
    Task<(List<Order> Items, int Total)> GetPagedAsync(
        OrderStatus? status, string? search, int page, int pageSize, CancellationToken ct = default);
} 