using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.DTOs;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Enums;

namespace SmartInventoryManagementAPI.Controllers;

using Microsoft.AspNetCore.Authorization;
using SmartInventoryManagementAPI.Authorization;

[ApiController]
[Route("api/[controller]")]
public class OrderController(IOrderService service) : ControllerBase
{
    [HttpPost]
   
    public async Task<ActionResult<OrderDto>> Create(CreateOrderDto dto)
    {
        var created = await service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetAll([FromQuery] OrderQuery query)
    => Ok(await service.GetAllAsync(query));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetById(int id) => Ok(await service.GetByIdAsync(id));

    [HttpPost("{id:int}/confirm")]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<ActionResult<OrderDto>> Confirm(int id) => Ok(await service.ConfirmAsync(id));

    [HttpPost("{id:int}/ship")]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<ActionResult<OrderDto>> Ship(int id) => Ok(await service.ShipAsync(id));

    [HttpPost("{id:int}/deliver")]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<ActionResult<OrderDto>> Deliver(int id) => Ok(await service.DeliverAsync(id));

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<ActionResult<OrderDto>> Cancel(int id) => Ok(await service.CancelAsync(id));
}