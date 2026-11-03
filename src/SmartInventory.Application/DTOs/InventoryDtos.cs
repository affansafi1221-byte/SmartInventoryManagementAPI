using System;
using System.Collections.Generic;
using System.Text;

namespace SmartInventory.Application.DTOs;

public record InventoryDto(
    int ProductId, string ProductName, string Sku,
    int QuantityOnHand, int ReorderLevel, bool IsLowStock);

public record StockChangeDto(int ProductId, int Quantity, string? Reference, string? Note);

public record AdjustStockDto(int ProductId, int NewQuantity, string? Note);

public record StockMovementDto(
    int Id, int ProductId, string ProductName, string Type,
    int Quantity, string? Reference, string? Note, DateTime CreatedAt);
