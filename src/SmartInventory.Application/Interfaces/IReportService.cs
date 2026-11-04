using System;
using System.Collections.Generic;
using System.Text;

using SmartInventory.Application.DTOs;

namespace SmartInventory.Application.Interfaces;

public interface IReportRepository
{
    Task<DashboardDto> GetDashboardAsync(DateTime todayStartUtc, CancellationToken ct = default);
    Task<SalesSummaryDto> GetSalesSummaryAsync(DateTime from, DateTime toExclusive, CancellationToken ct = default);
    Task<List<TopProductDto>> GetTopProductsAsync(DateTime from, DateTime toExclusive, int take, CancellationToken ct = default);
}

public interface IReportService
{
    Task<DashboardDto> GetDashboardAsync();
    Task<SalesSummaryDto> GetSalesSummaryAsync(DateTime? from, DateTime? to);
    Task<List<TopProductDto>> GetTopProductsAsync(int take, DateTime? from, DateTime? to);
}