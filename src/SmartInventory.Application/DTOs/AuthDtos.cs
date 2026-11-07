using System;
using System.Collections.Generic;
using System.Text;

namespace SmartInventory.Application.DTOs;

public record CreateUserDto(string FullName, string Email, string Password, int RoleId);
public record ChangeRoleDto(int RoleId);
public record SetActiveDto(bool IsActive);
public record RegisterDto(string FullName, string Email, string Password);
public record LoginDto(string Email, string Password);
public record UserDto(int Id, string FullName, string Email, string Role);
public record AuthResponseDto(string Token, DateTime ExpiresAt, UserDto User);



