using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.DTOs;
using SmartInventory.Application.Interfaces;

namespace SmartInventoryManagementAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController(IInventoryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<InventoryDto>>> GetAll()
        => Ok(await service.GetAllAsync());

    [HttpGet("{productId:int}")]
    public async Task<ActionResult<InventoryDto>> GetByProductId(int productId)
        => Ok(await service.GetByProductIdAsync(productId));

    [HttpGet("low-stock")]
    public async Task<ActionResult<List<InventoryDto>>> GetLowStock()
        => Ok(await service.GetLowStockAsync());

    [HttpPost("stock-in")]
    public async Task<ActionResult<InventoryDto>> StockIn(StockChangeDto dto)
        => Ok(await service.StockInAsync(dto));

    [HttpPost("stock-out")]
    public async Task<ActionResult<InventoryDto>> StockOut(StockChangeDto dto)
        => Ok(await service.StockOutAsync(dto));

    [HttpPost("adjust")]
    public async Task<ActionResult<InventoryDto>> Adjust(AdjustStockDto dto)
        => Ok(await service.AdjustAsync(dto));

    [HttpGet("movements")]
    public async Task<ActionResult<List<StockMovementDto>>> GetMovements(
        [FromQuery] int? productId, [FromQuery] int take = 50)
        => Ok(await service.GetMovementsAsync(productId, take));
}