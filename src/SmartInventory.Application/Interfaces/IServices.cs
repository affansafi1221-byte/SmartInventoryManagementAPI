using SmartInventory.Application.DTOs;
using SmartInventory.Domain.Enums;

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
public interface IOrderService
{
    Task<OrderDto> CreateAsync(CreateOrderDto dto);
    Task<List<OrderDto>> GetAllAsync(OrderStatus? status);
    Task<OrderDto> GetByIdAsync(int id);
    Task<OrderDto> ConfirmAsync(int id);
    Task<OrderDto> ShipAsync(int id);
    Task<OrderDto> DeliverAsync(int id);
    Task<OrderDto> CancelAsync(int id);
}
public interface IPaymentService
{
    Task<OrderPaymentsDto> CreateAsync(int orderId, CreatePaymentDto dto);
    Task<OrderPaymentsDto> GetByOrderAsync(int orderId);
}