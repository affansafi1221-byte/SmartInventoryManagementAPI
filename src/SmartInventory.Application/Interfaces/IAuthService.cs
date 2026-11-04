using System;
using System.Collections.Generic;
using System.Text;
using SmartInventory.Application.DTOs;
using SmartInventory.Domain.Entities;

namespace SmartInventory.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) Create(User user);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdWithRoleAsync(int id, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
}

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<UserDto> GetCurrentAsync(int userId);
}