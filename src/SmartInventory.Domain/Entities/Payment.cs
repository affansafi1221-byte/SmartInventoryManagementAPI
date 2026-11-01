using System;
using System.Collections.Generic;
using System.Text;
using SmartInventory.Domain.Common;
using SmartInventory.Domain.Enums;

namespace SmartInventory.Domain.Entities;

public class Payment : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime? PaidAt { get; set; }
} 
