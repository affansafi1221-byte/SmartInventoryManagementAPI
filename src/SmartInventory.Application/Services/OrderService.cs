using SmartInventory.Application.DTOs;
using SmartInventory.Application.Exceptions;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Entities;
using SmartInventory.Domain.Enums;

namespace SmartInventory.Application.Services;

public class OrderService(
    IOrderRepository orders,
    IInventoryRepository inventories,
    IStockMovementRepository movements,
    IUnitOfWork uow) : IOrderService
{
    public async Task<OrderDto> CreateAsync(CreateOrderDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerName))
            throw new ValidationException("Customer name is required.");
        if (dto.Items is null || dto.Items.Count == 0)
            throw new ValidationException("Order must contain at least one item.");
        if (dto.Items.Any(i => i.Quantity <= 0))
            throw new ValidationException("Item quantity must be greater than zero.");

        // Same product do baar aaye to quantity jama kar do
        var lines = dto.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        var order = new Order
        {
            OrderNumber = GenerateOrderNumber(),
            CustomerName = dto.CustomerName.Trim(),
            CustomerEmail = dto.CustomerEmail?.Trim(),
            Status = OrderStatus.Pending
        };

        foreach (var line in lines)
        {
            var inv = await inventories.GetByProductIdAsync(line.ProductId)
                      ?? throw new NotFoundException($"Product {line.ProductId} not found.");

            if (!inv.Product.IsActive)
                throw new ValidationException($"Product '{inv.Product.Name}' is inactive.");
            if (inv.QuantityOnHand < line.Quantity)
                throw new ValidationException(
                    $"Insufficient stock for '{inv.Product.Name}'. Available: {inv.QuantityOnHand}, requested: {line.Quantity}.");

            inv.QuantityOnHand -= line.Quantity;
            inv.UpdatedAt = DateTime.UtcNow;

            order.Items.Add(new OrderItem
            {
                ProductId = inv.ProductId,
                Product = inv.Product,
                Quantity = line.Quantity,
                UnitPrice = inv.Product.Price // price snapshot
            });

            await movements.AddAsync(new StockMovement
            {
                ProductId = inv.ProductId,
                Type = StockMovementType.Out,
                Quantity = line.Quantity,
                Reference = order.OrderNumber,
                Note = "Order created"
            });
        }

        order.TotalAmount = order.Items.Sum(i => i.LineTotal);
        await orders.AddAsync(order);
        await uow.SaveChangesAsync(); // order + items + stock + movements, ek transaction
        return Map(order);
    }

    public async Task<List<OrderDto>> GetAllAsync(OrderStatus? status)
        => (await orders.GetAllWithDetailsAsync(status)).Select(Map).ToList();

    public async Task<OrderDto> GetByIdAsync(int id) => Map(await GetOrThrowAsync(id));

    public Task<OrderDto> ConfirmAsync(int id) => MoveAsync(id, OrderStatus.Pending, OrderStatus.Confirmed);
    public Task<OrderDto> ShipAsync(int id) => MoveAsync(id, OrderStatus.Confirmed, OrderStatus.Shipped);
    public Task<OrderDto> DeliverAsync(int id) => MoveAsync(id, OrderStatus.Shipped, OrderStatus.Delivered);

    public async Task<OrderDto> CancelAsync(int id)
    {
        var order = await GetOrThrowAsync(id);

        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
            throw new ValidationException($"Order cannot be cancelled when status is {order.Status}.");

        foreach (var item in order.Items)
        {
            var inv = await inventories.GetByProductIdAsync(item.ProductId)
                      ?? throw new NotFoundException($"Inventory for product {item.ProductId} not found.");

            inv.QuantityOnHand += item.Quantity;
            inv.UpdatedAt = DateTime.UtcNow;

            await movements.AddAsync(new StockMovement
            {
                ProductId = item.ProductId,
                Type = StockMovementType.Return,
                Quantity = item.Quantity,
                Reference = order.OrderNumber,
                Note = "Order cancelled"
            });
        }
        foreach (var payment in order.Payments.Where(p => p.Status == PaymentStatus.Completed))
        {
            payment.Status = PaymentStatus.Refunded;
            payment.UpdatedAt = DateTime.UtcNow;
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync();
        return Map(order);
    }

    private async Task<OrderDto> MoveAsync(int id, OrderStatus required, OrderStatus next)
    {
        var order = await GetOrThrowAsync(id);
        if (order.Status != required)
            throw new ValidationException(
                $"Order must be {required} to become {next}. Current status: {order.Status}.");

        order.Status = next;
        order.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync();
        return Map(order);
    }

    private async Task<Order> GetOrThrowAsync(int id)
        => await orders.GetByIdWithDetailsAsync(id)
           ?? throw new NotFoundException($"Order {id} not found.");

    private static string GenerateOrderNumber()
        => $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private static OrderDto Map(Order o) => new(
        o.Id, o.OrderNumber, o.CustomerName, o.CustomerEmail, o.Status.ToString(),
        o.TotalAmount, o.CreatedAt,
        o.Items.Select(i => new OrderItemDto(
            i.ProductId, i.Product.Name, i.Product.Sku, i.Quantity, i.UnitPrice, i.LineTotal)).ToList());
}