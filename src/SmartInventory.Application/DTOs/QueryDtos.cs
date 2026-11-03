using System;
using System.Collections.Generic;
using System.Text;

using SmartInventory.Domain.Enums;

namespace SmartInventory.Application.DTOs;

public record ProductQuery(string? Search, int? CategoryId, int Page = 1, int PageSize = 10); 

public record OrderQuery(OrderStatus? Status, string? Search, int Page = 1, int PageSize = 10);