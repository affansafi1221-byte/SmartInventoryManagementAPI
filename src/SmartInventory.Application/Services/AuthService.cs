using System;
using System.Collections.Generic;
using System.Text;

using System.Net.Mail;
using SmartInventory.Application.DTOs;
using SmartInventory.Application.Exceptions;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Constants;
using SmartInventory.Domain.Entities;

namespace SmartInventory.Application.Services;

public class AuthService(
    IUserRepository users,
    IPasswordHasher hasher,
    IJwtTokenService jwt,
    IUnitOfWork uow) : IAuthService
{
    public async Task<UserDto> RegisterAsync(RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new ValidationException("Full name is required.");

        var email = Normalize(dto.Email);
        if (!MailAddress.TryCreate(email, out _))
            throw new ValidationException("A valid email is required.");

        ValidatePassword(dto.Password);

        if (await users.EmailExistsAsync(email))
            throw new ValidationException("Email is already registered.");

        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            PasswordHash = hasher.Hash(dto.Password),
            RoleId = RoleIds.Staff // public register se sirf Staff
        };

        await users.AddAsync(user);
        await uow.SaveChangesAsync();

        var saved = await users.GetByIdWithRoleAsync(user.Id);
        return Map(saved!);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await users.GetByEmailAsync(Normalize(dto.Email));

        // Dono cases (email galat / password galat) ka ek hi message, taake attacker ko hint na mile
        if (user is null || !user.IsActive || !hasher.Verify(dto.Password ?? "", user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        var (token, expires) = jwt.Create(user);
        return new AuthResponseDto(token, expires, Map(user));
    }

    public async Task<UserDto> GetCurrentAsync(int userId)
    {
        var user = await users.GetByIdWithRoleAsync(userId)
                   ?? throw new NotFoundException("User not found.");
        if (!user.IsActive) throw new UnauthorizedException("Account is disabled.");
        return Map(user);
    }

    private static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();

    internal static void ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            throw new ValidationException("Password must be at least 8 characters.");
        if (password.Length > 100)
            throw new ValidationException("Password is too long.");
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new ValidationException("Password must contain at least one letter and one digit.");
    }

    private static UserDto Map(User u) => new(u.Id, u.FullName, u.Email, u.Role.Name);
}