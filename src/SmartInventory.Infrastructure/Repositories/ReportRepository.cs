using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.DTOs;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Enums;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Repositories;

public class ReportRepository(AppDbContext db) : IReportRepository
{
    public async Task<DashboardDto> GetDashboardAsync(DateTime todayStartUtc, CancellationToken ct = default)
    {
        var inventory = db.Inventories.AsNoTracking().Where(i => i.Product.IsActive);

        var totalProducts = await db.Products.CountAsync(p => p.IsActive, ct);
        var lowStock = await inventory.CountAsync(i => i.QuantityOnHand <= i.Product.ReorderLevel, ct);
        var outOfStock = await inventory.CountAsync(i => i.QuantityOnHand == 0, ct);
        var costValue = await inventory.SumAsync(i => (decimal?)(i.QuantityOnHand * i.Product.CostPrice), ct) ?? 0;
        var retailValue = await inventory.SumAsync(i => (decimal?)(i.QuantityOnHand * i.Product.Price), ct) ?? 0;

        var totalOrders = await db.Orders.CountAsync(ct);
        var pendingOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending, ct);

        var today = db.Orders.Where(o => o.Status != OrderStatus.Cancelled && o.CreatedAt >= todayStartUtc);
        var ordersToday = await today.CountAsync(ct);
        var salesToday = await today.SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0;

        var totalPaid = await db.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0;

        return new DashboardDto(
            totalProducts, lowStock, outOfStock, costValue, retailValue,
            totalOrders, pendingOrders, ordersToday, salesToday, totalPaid);
    }

    public async Task<SalesSummaryDto> GetSalesSummaryAsync(DateTime from, DateTime toExclusive, CancellationToken ct = default)
    {
        var orders = db.Orders.AsNoTracking()
            .Where(o => o.Status != OrderStatus.Cancelled && o.CreatedAt >= from && o.CreatedAt < toExclusive);

        var count = await orders.CountAsync(ct);
        var total = await orders.SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0;

        var itemsSold = await db.OrderItems.AsNoTracking()
            .Where(i => i.Order.Status != OrderStatus.Cancelled
                        && i.Order.CreatedAt >= from && i.Order.CreatedAt < toExclusive)
            .SumAsync(i => (int?)i.Quantity, ct) ?? 0;

        var daily = await orders
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Orders = g.Count(), Sales = g.Sum(o => o.TotalAmount) })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        var average = count == 0 ? 0 : Math.Round(total / count, 2);

        return new SalesSummaryDto(
            from, toExclusive.AddDays(-1), count, total, itemsSold, average,
            daily.Select(d => new DailySalesDto(d.Date, d.Orders, d.Sales)).ToList());
    }

    public async Task<List<TopProductDto>> GetTopProductsAsync(DateTime from, DateTime toExclusive, int take, CancellationToken ct = default)
    {
        var rows = await db.OrderItems.AsNoTracking()
            .Where(i => i.Order.Status != OrderStatus.Cancelled
                        && i.Order.CreatedAt >= from && i.Order.CreatedAt < toExclusive)
            .GroupBy(i => new { i.ProductId, i.Product.Name, i.Product.Sku })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Name,
                g.Key.Sku,
                Qty = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Quantity * x.UnitPrice)
            })
            .OrderByDescending(x => x.Qty)
            .Take(take)
            .ToListAsync(ct);

        return rows.Select(r => new TopProductDto(r.ProductId, r.Name, r.Sku, r.Qty, r.Revenue)).ToList();
    }
}