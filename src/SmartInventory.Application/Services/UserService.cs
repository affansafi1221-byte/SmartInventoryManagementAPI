using SmartInventory.Application.DTOs;
using SmartInventory.Application.Exceptions;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;
using static SmartInventory.Application.Interfaces.IUserRepository;

namespace SmartInventory.Application.Services;

public class UserService(
    IUserRepository users,
    IRepository<Role> roles,
    IPasswordHasher hasher,
    ICurrentUserService currentUser,
    IUnitOfWork uow) : IUserService
{
    public async Task<List<UserDto>> GetAllAsync()
        => (await users.GetAllWithRoleAsync()).Select(Map).ToList();

    public async Task<UserDto> CreateAsync(CreateUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new ValidationException("Full name is required.");

        var email = (dto.Email ?? "").Trim().ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out _))
            throw new ValidationException("A valid email is required.");

        AuthService.ValidatePassword(dto.Password);

        var role = await roles.GetByIdAsync(dto.RoleId)
                   ?? throw new ValidationException("Invalid role.");

        if (await users.EmailExistsAsync(email))
            throw new ValidationException("Email is already registered.");

        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            PasswordHash = hasher.Hash(dto.Password),
            Role = role
        };

        await users.AddAsync(user);
        await uow.SaveChangesAsync();
        return Map(user);
    }

    public async Task<UserDto> ChangeRoleAsync(int id, int roleId)
    {
        if (currentUser.UserId == id)
            throw new ValidationException("You cannot change your own role.");

        var role = await roles.GetByIdAsync(roleId)
                   ?? throw new ValidationException("Invalid role.");
        var user = await users.GetByIdWithRoleAsync(id)
                   ?? throw new NotFoundException($"User {id} not found.");

        user.Role = role;
        user.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync();
        return Map(user);
    }

    public async Task<UserDto> SetActiveAsync(int id, bool isActive)
    {
        if (currentUser.UserId == id && !isActive)
            throw new ValidationException("You cannot deactivate your own account.");

        var user = await users.GetByIdWithRoleAsync(id)
                   ?? throw new NotFoundException($"User {id} not found.");

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync();
        return Map(user);
    }

    private static UserDto Map(User u) => new(u.Id, u.FullName, u.Email, u.Role.Name);
}