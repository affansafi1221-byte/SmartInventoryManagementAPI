using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.DTOs;
using SmartInventory.Application.Interfaces;
using SmartInventoryManagementAPI.Authorization;

namespace SmartInventoryManagementAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.AdminOrManager)]
public class ReportsController(IReportService service) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard()
        => Ok(await service.GetDashboardAsync());

    [HttpGet("sales-summary")]
    public async Task<ActionResult<SalesSummaryDto>> SalesSummary(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await service.GetSalesSummaryAsync(from, to));

    [HttpGet("top-products")]
    public async Task<ActionResult<List<TopProductDto>>> TopProducts(
        [FromQuery] int take = 5, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        => Ok(await service.GetTopProductsAsync(take, from, to));
}