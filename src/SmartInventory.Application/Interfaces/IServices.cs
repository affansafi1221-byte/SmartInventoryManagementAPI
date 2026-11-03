using System;
using System.Collections.Generic;
using System.Text;

using SmartInventory.Application.DTOs;

namespace SmartInventory.Application.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync();
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto);
}

public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync();
    Task<ProductDto?> GetByIdAsync(int id);
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task<ProductDto?> UpdateAsync(int id, UpdateProductDto dto);
    Task<bool> DeleteAsync(int id);
}
public interface IInventoryService
{
    Task<List<InventoryDto>> GetAllAsync();
    Task<InventoryDto> GetByProductIdAsync(int productId);
    Task<List<InventoryDto>> GetLowStockAsync();
    Task<InventoryDto> StockInAsync(StockChangeDto dto);
    Task<InventoryDto> StockOutAsync(StockChangeDto dto);
    Task<InventoryDto> AdjustAsync(AdjustStockDto dto);
    Task<List<StockMovementDto>> GetMovementsAsync(int? productId, int take = 50);
} 
