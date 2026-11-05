using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.DTOs;
using SmartInventory.Application.Interfaces;
using SmartInventoryManagementAPI.Authorization;
using static SmartInventory.Application.Interfaces.IUserRepository;

namespace SmartInventoryManagementAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin)]
public class UsersController(IUserService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll() => Ok(await service.GetAllAsync());

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserDto dto) => Ok(await service.CreateAsync(dto));

    [HttpPut("{id:int}/role")]
    public async Task<ActionResult<UserDto>> ChangeRole(int id, ChangeRoleDto dto)
        => Ok(await service.ChangeRoleAsync(id, dto.RoleId));

    [HttpPut("{id:int}/active")]
    public async Task<ActionResult<UserDto>> SetActive(int id, SetActiveDto dto)
        => Ok(await service.SetActiveAsync(id, dto.IsActive));
}