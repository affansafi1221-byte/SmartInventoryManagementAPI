using System;
using System.Collections.Generic;
using System.Text;
using SmartInventory.Domain.Common;

namespace SmartInventory.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}