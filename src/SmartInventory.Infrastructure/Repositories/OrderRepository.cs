using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Entities;
using SmartInventory.Domain.Enums;
using SmartInventory.Infrastructure.Data;



namespace SmartInventory.Infrastructure.Repositories;

public class OrderRepository(AppDbContext db) : Repository<Order>(db), IOrderRepository
{
    public Task<Order?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default)
        => Set.Include(o => o.Items).ThenInclude(i => i.Product)
              .Include(o => o.Payments)
              .AsSplitQuery()
              .FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<List<Order>> GetAllWithDetailsAsync(OrderStatus? status, CancellationToken ct = default)
        => Set.AsNoTracking()
              .Include(o => o.Items).ThenInclude(i => i.Product)
              .Where(o => status == null || o.Status == status)
              .OrderByDescending(o => o.CreatedAt)
              .ToListAsync(ct);
}