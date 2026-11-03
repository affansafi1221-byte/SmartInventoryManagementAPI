using System;
using System.Collections.Generic;
using System.Text;
using SmartInventory.Domain.Entities;
using SmartInventory.Domain.Enums;

namespace SmartInventory.Application.Interfaces;

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
    Task<List<Order>> GetAllWithDetailsAsync(OrderStatus? status, CancellationToken ct = default);
} 