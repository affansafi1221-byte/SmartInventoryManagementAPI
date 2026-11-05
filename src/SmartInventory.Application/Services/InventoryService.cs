using SmartInventory.Application.DTOs;
using SmartInventory.Application.Exceptions;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Entities;
using SmartInventory.Domain.Enums;

namespace SmartInventory.Application.Services;


public class InventoryService(
    IInventoryRepository inventories,
    IStockMovementRepository movements,
    IUnitOfWork uow) : IInventoryService
{
    public async Task<List<InventoryDto>> GetAllAsync()
        => (await inventories.GetAllWithProductAsync()).Select(Map).ToList();

    public async Task<InventoryDto> GetByProductIdAsync(int productId)
        => Map(await GetOrThrowAsync(productId));

    public async Task<List<InventoryDto>> GetLowStockAsync()
        => (await inventories.GetAllWithProductAsync())
            .Where(i => i.QuantityOnHand <= i.Product.ReorderLevel)
            .Select(Map).ToList();

    public async Task<InventoryDto> StockInAsync(StockChangeDto dto)
    {
        if (dto.Quantity <= 0) throw new ValidationException("Quantity must be greater than zero.");

        var inv = await GetOrThrowAsync(dto.ProductId);
        inv.QuantityOnHand += dto.Quantity;
        inv.UpdatedAt = DateTime.UtcNow;

        await RecordAsync(inv, StockMovementType.In, dto.Quantity, dto.Reference, dto.Note);
        await uow.SaveChangesAsync();
        return Map(inv);
    }
    UserId = currentUser.UserId,

    public async Task<InventoryDto> StockOutAsync(StockChangeDto dto)
    {
        if (dto.Quantity <= 0) throw new ValidationException("Quantity must be greater than zero.");

        var inv = await GetOrThrowAsync(dto.ProductId);
        if (inv.QuantityOnHand < dto.Quantity)
            throw new ValidationException(
                $"Insufficient stock. Available: {inv.QuantityOnHand}, requested: {dto.Quantity}.");

        inv.QuantityOnHand -= dto.Quantity;
        inv.UpdatedAt = DateTime.UtcNow;

        await RecordAsync(inv, StockMovementType.Out, dto.Quantity, dto.Reference, dto.Note);
        await uow.SaveChangesAsync();
        
        return Map(inv);
    }

    public async Task<InventoryDto> AdjustAsync(AdjustStockDto dto)
    {
        if (dto.NewQuantity < 0) throw new ValidationException("Quantity cannot be negative.");

        var inv = await GetOrThrowAsync(dto.ProductId);
        var difference = dto.NewQuantity - inv.QuantityOnHand;
        inv.QuantityOnHand = dto.NewQuantity;
        inv.UpdatedAt = DateTime.UtcNow;

        // Adjustment mein Quantity = farq (positive ya negative)
        await RecordAsync(inv, StockMovementType.Adjustment, difference, null, dto.Note);
        await uow.SaveChangesAsync();
        return Map(inv);
    }

    public async Task<List<StockMovementDto>> GetMovementsAsync(int? productId, int take = 50)
    {
        take = Math.Clamp(take, 1, 200);
        var items = await movements.GetHistoryAsync(productId, take);
        return items.Select(m => new StockMovementDto(
            m.Id, m.ProductId, m.Product.Name, m.Type.ToString(),
            m.Quantity, m.Reference, m.Note, m.CreatedAt)).ToList();
    }

    private async Task<Inventory> GetOrThrowAsync(int productId)
    {
        var inv = await inventories.GetByProductIdAsync(productId)
                  ?? throw new NotFoundException($"Inventory for product {productId} not found.");
        if (!inv.Product.IsActive)
            throw new ValidationException("Product is inactive.");
        return inv;
    }

    private Task RecordAsync(Inventory inv, StockMovementType type, int qty, string? reference, string? note)
        => movements.AddAsync(new StockMovement
        {
            ProductId = inv.ProductId,
            Type = type,
            Quantity = qty,
            Reference = reference,
            Note = note,
            UserId = currentUser.UserId
        });

    private static InventoryDto Map(Inventory i) => new(
        i.ProductId, i.Product.Name, i.Product.Sku, i.QuantityOnHand,
        i.Product.ReorderLevel, i.QuantityOnHand <= i.Product.ReorderLevel);
}