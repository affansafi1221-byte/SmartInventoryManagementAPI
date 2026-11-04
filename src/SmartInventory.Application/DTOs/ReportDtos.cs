using System;
using System.Collections.Generic;
using System.Text;

namespace SmartInventory.Application.DTOs;

public record DashboardDto(
    int TotalProducts, int LowStockCount, int OutOfStockCount,
    decimal InventoryCostValue, decimal InventoryRetailValue,
    int TotalOrders, int PendingOrders, int OrdersToday, decimal SalesToday,
    decimal TotalPaid);

public record DailySalesDto(DateTime Date, int Orders, decimal Sales);

public record SalesSummaryDto(
    DateTime From, DateTime To, int OrderCount, decimal TotalSales,
    int ItemsSold, decimal AverageOrderValue, List<DailySalesDto> Daily);

public record TopProductDto(int ProductId, string ProductName, string Sku, int QuantitySold, decimal Revenue);