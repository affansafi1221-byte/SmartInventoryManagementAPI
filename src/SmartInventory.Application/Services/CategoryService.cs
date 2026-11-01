using System;
using System.Collections.Generic;
using System.Text;
using SmartInventory.Application.DTOs;
using SmartInventory.Application.Exceptions;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Entities;

namespace SmartInventory.Application.Services;

public class CategoryService(IRepository<Category> repo, IUnitOfWork uow) : ICategoryService
{
    public async Task<List<CategoryDto>> GetAllAsync()
    {
        var items = await repo.GetAllAsync();
        return items.Select(c => new CategoryDto(c.Id, c.Name, c.Description)).ToList();
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ValidationException("Category name is required.");

        var category = new Category { Name = dto.Name.Trim(), Description = dto.Description };
        await repo.AddAsync(category);
        await uow.SaveChangesAsync();
        return new CategoryDto(category.Id, category.Name, category.Description);
    }
} 