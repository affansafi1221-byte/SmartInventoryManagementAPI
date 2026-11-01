using System;
using System.Collections.Generic;
using System.Text;
using SmartInventory.Domain.Common;

namespace SmartInventory.Domain.Entities;

public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => Quantity * UnitPrice; // computed, DB mein store nahi hoga
}
