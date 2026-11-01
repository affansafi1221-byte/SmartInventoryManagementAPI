using System;
using System.Collections.Generic;
using System.Text;
using SmartInventory.Domain.Common;
using SmartInventory.Domain.Enums;

namespace SmartInventory.Domain.Entities;

public class StockMovement : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public StockMovementType Type { get; set; }
    public int Quantity { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
}