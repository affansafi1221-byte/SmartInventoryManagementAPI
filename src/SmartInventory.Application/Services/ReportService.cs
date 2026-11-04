using System;
using System.Collections.Generic;
using System.Text;

using SmartInventory.Application.DTOs;
using SmartInventory.Application.Exceptions;
using SmartInventory.Application.Interfaces;

namespace SmartInventory.Application.Services;

public class ReportService(IReportRepository repo) : IReportService
{
    public Task<DashboardDto> GetDashboardAsync()
        => repo.GetDashboardAsync(DateTime.UtcNow.Date);

    public Task<SalesSummaryDto> GetSalesSummaryAsync(DateTime? from, DateTime? to)
    {
        var (start, endExclusive) = ResolveRange(from, to);
        return repo.GetSalesSummaryAsync(start, endExclusive);
    }

    public Task<List<TopProductDto>> GetTopProductsAsync(int take, DateTime? from, DateTime? to)
    {
        var (start, endExclusive) = ResolveRange(from, to);
        return repo.GetTopProductsAsync(start, endExclusive, Math.Clamp(take, 1, 50));
    }

    private static (DateTime Start, DateTime EndExclusive) ResolveRange(DateTime? from, DateTime? to)
    {
        var end = (to ?? DateTime.UtcNow).Date;
        var start = (from ?? end.AddDays(-29)).Date;

        if (start > end)
            throw new ValidationException("'from' date must be on or before 'to' date.");
        if ((end - start).TotalDays > 366)
            throw new ValidationException("Date range cannot exceed one year.");

        return (start, end.AddDays(1));
    }
}