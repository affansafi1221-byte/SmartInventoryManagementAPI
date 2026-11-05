using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.DTOs;
using SmartInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using SmartInventoryManagementAPI.Authorization;

namespace SmartInventoryManagementAPI.Controllers;

[ApiController]
[Route("api/[controller]")]

public class CategoriesController(ICategoryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAll() => Ok(await service.GetAllAsync());

    [HttpPost]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryDto dto)
    {
        var created = await service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
    }
}