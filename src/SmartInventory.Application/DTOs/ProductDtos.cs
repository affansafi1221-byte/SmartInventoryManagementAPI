using System;
using System.Collections.Generic;
using System.Text;

namespace SmartInventory.Application.DTOs;

public record ProductDto(
    int Id, string Name, string Sku, string? Description,
    decimal Price, decimal CostPrice, int ReorderLevel,
    bool IsActive, int CategoryId, string CategoryName, int QuantityOnHand);

public record CreateProductDto(
    string Name, string Sku, string? Description,
    decimal Price, decimal CostPrice, int ReorderLevel, int CategoryId);

public record UpdateProductDto(
    string Name, string? Description,
    decimal Price, decimal CostPrice, int ReorderLevel,
    bool IsActive, int CategoryId);