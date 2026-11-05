using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.DTOs;
using SmartInventory.Application.Interfaces;

using Microsoft.AspNetCore.Authorization;
using SmartInventoryManagementAPI.Authorization;
namespace SmartInventoryManagementAPI.Controllers;

[ApiController]
[Route("api/orders/{orderId:int}/payments")]
public class PaymentsController(IPaymentService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderPaymentsDto>> Create(int orderId, CreatePaymentDto dto)
        => Ok(await service.CreateAsync(orderId, dto));

    [HttpGet]
    public async Task<ActionResult<OrderPaymentsDto>> Get(int orderId)
        => Ok(await service.GetByOrderAsync(orderId));
}