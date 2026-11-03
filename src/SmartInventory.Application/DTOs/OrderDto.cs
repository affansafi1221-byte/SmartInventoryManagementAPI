using System;
using System.Collections.Generic;
using System.Text;

namespace SmartInventory.Application.DTOs;

public record CreateOrderItemDto(int ProductId, int Quantity);

public record CreateOrderDto(string CustomerName, string? CustomerEmail, List<CreateOrderItemDto> Items);

public record OrderItemDto(
    int ProductId, string ProductName, string Sku,
    int Quantity, decimal UnitPrice, decimal LineTotal);

public record OrderDto(
    int Id, string OrderNumber, string CustomerName, string? CustomerEmail,
    string Status, decimal TotalAmount, DateTime CreatedAt, List<OrderItemDto> Items);