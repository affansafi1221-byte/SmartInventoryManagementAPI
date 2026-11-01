using System;
using System.Collections.Generic;
using System.Text;
using SmartInventory.Domain.Common;

namespace SmartInventory.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal CostPrice { get; set; }
    public int ReorderLevel { get; set; } = 10;
    public bool IsActive { get; set; } = true;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public Inventory? Inventory { get; set; }
}