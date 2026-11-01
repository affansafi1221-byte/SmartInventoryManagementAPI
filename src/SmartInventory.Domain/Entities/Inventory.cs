using System;
using System.Collections.Generic;
using System.Text;
using SmartInventory.Domain.Common;

namespace SmartInventory.Domain.Entities;

public class Inventory : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int QuantityOnHand { get; set; }
}