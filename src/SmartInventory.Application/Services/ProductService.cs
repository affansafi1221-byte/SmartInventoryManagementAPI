using System;
using System.Collections.Generic;
using System.Text;
using SmartInventory.Application.DTOs;
using SmartInventory.Application.Exceptions;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Entities;

namespace SmartInventory.Application.Services;

public class ProductService(
    IProductRepository products,
    IRepository<Category> categories,
    IUnitOfWork uow) : IProductService
{
    public async Task<List<ProductDto>> GetAllAsync()
        => (await products.GetAllWithDetailsAsync()).Select(Map).ToList();

    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        var p = await products.GetByIdWithDetailsAsync(id);
        return p is null ? null : Map(p);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationException("Product name is required.");
        if (string.IsNullOrWhiteSpace(dto.Sku)) throw new ValidationException("SKU is required.");
        if (dto.Price < 0 || dto.CostPrice < 0) throw new ValidationException("Price cannot be negative.");
        if (await categories.GetByIdAsync(dto.CategoryId) is null)
            throw new NotFoundException($"Category {dto.CategoryId} not found.");
        if (await products.SkuExistsAsync(dto.Sku.Trim()))
            throw new ValidationException($"SKU '{dto.Sku}' already exists.");

        var product = new Product
        {
            Name = dto.Name.Trim(),
            Sku = dto.Sku.Trim().ToUpperInvariant(),
            Description = dto.Description,
            Price = dto.Price,
            CostPrice = dto.CostPrice,
            ReorderLevel = dto.ReorderLevel,
            CategoryId = dto.CategoryId,
            Inventory = new Inventory { QuantityOnHand = 0 }
        };

        await products.AddAsync(product);
        await uow.SaveChangesAsync();

        var saved = await products.GetByIdWithDetailsAsync(product.Id);
        return Map(saved!);
    }

    public async Task<ProductDto?> UpdateAsync(int id, UpdateProductDto dto)
    {
        var product = await products.GetByIdWithDetailsAsync(id);
        if (product is null) return null;

        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationException("Product name is required.");
        if (dto.Price < 0 || dto.CostPrice < 0) throw new ValidationException("Price cannot be negative.");
        if (await categories.GetByIdAsync(dto.CategoryId) is null)
            throw new NotFoundException($"Category {dto.CategoryId} not found.");

        product.Name = dto.Name.Trim();
        product.Description = dto.Description;
        product.Price = dto.Price;
        product.CostPrice = dto.CostPrice;
        product.ReorderLevel = dto.ReorderLevel;
        product.IsActive = dto.IsActive;
        product.CategoryId = dto.CategoryId;
        product.UpdatedAt = DateTime.UtcNow;

        await uow.SaveChangesAsync();

        var updated = await products.GetByIdWithDetailsAsync(id);
        return Map(updated!);
    }

    // Soft delete: order history safe rehti hai
    public async Task<bool> DeleteAsync(int id)
    {
        var product = await products.GetByIdAsync(id);
        if (product is null) return false;

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync();
        return true;
    }

    private static ProductDto Map(Product p) => new(
        p.Id, p.Name, p.Sku, p.Description, p.Price, p.CostPrice, p.ReorderLevel,
        p.IsActive, p.CategoryId, p.Category.Name, p.Inventory?.QuantityOnHand ?? 0);
} 