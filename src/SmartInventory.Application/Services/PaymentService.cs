using System;
using System.Collections.Generic;
using System.Text;
using SmartInventory.Application.DTOs;
using SmartInventory.Application.Exceptions;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Entities;
using SmartInventory.Domain.Enums;

namespace SmartInventory.Application.Services;

public class PaymentService(IOrderRepository orders, IUnitOfWork uow) : IPaymentService
{
    public async Task<OrderPaymentsDto> CreateAsync(int orderId, CreatePaymentDto dto)
    {
        if (dto.Amount <= 0)
            throw new ValidationException("Payment amount must be greater than zero.");

        var order = await GetOrderAsync(orderId);
        if (order.Status == OrderStatus.Cancelled)
            throw new ValidationException("Cannot add payment to a cancelled order.");

        var balance = order.TotalAmount - PaidAmount(order);
        if (dto.Amount > balance)
            throw new ValidationException($"Payment exceeds balance due. Balance due: {balance}.");

        order.Payments.Add(new Payment
        {
            Amount = dto.Amount,
            Method = dto.Method,
            Status = PaymentStatus.Completed,
            PaidAt = DateTime.UtcNow
        });
        order.UpdatedAt = DateTime.UtcNow;

        await uow.SaveChangesAsync();
        return Map(order);
    }

    public async Task<OrderPaymentsDto> GetByOrderAsync(int orderId)
        => Map(await GetOrderAsync(orderId));

    private async Task<Order> GetOrderAsync(int orderId)
        => await orders.GetByIdWithDetailsAsync(orderId)
           ?? throw new NotFoundException($"Order {orderId} not found.");

    private static decimal PaidAmount(Order o)
        => o.Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);

    private static OrderPaymentsDto Map(Order o)
    {
        var paid = PaidAmount(o);
        var hasRefund = o.Payments.Any(p => p.Status == PaymentStatus.Refunded);

        var state = paid <= 0 ? (hasRefund ? "Refunded" : "Unpaid")
                  : paid < o.TotalAmount ? "PartiallyPaid"
                  : "Paid";

        return new OrderPaymentsDto(
            o.Id, o.OrderNumber, o.TotalAmount, paid, o.TotalAmount - paid, state,
            o.Payments.OrderBy(p => p.CreatedAt)
                .Select(p => new PaymentDto(p.Id, p.Amount, p.Method.ToString(), p.Status.ToString(), p.PaidAt))
                .ToList());
    }
}