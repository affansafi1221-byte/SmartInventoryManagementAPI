using System;
using System.Collections.Generic;
using System.Text;
using SmartInventory.Domain.Enums;

namespace SmartInventory.Application.DTOs;

public record CreatePaymentDto(decimal Amount, PaymentMethod Method);

public record PaymentDto
 (int Id, decimal Amount, string Method, string Status, DateTime? PaidAt);
        
public record OrderPaymentsDto(
    int OrderId, string OrderNumber, decimal TotalAmount,
    decimal PaidAmount, decimal BalanceDue, string PaymentState,
    List<PaymentDto> Payments);
