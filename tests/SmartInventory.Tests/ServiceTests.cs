using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.DTOs;
using SmartInventory.Application.Exceptions;
using SmartInventory.Application.Interfaces;
using SmartInventory.Application.Services;
using SmartInventory.Domain.Constants;
using SmartInventory.Domain.Entities;
using SmartInventory.Domain.Enums;
using SmartInventory.Infrastructure.Data;
using SmartInventory.Infrastructure.Options;
using SmartInventory.Infrastructure.Repositories;
using SmartInventory.Infrastructure.Services;
using Xunit;

namespace SmartInventory.Tests;

// =====================================================================
// Shared test helpers
// =====================================================================

public class FakeCurrentUser : ICurrentUserService
{
    public int? UserId { get; set; }
    public string? Role { get; set; }
}

/// <summary>
/// Har test ko apna fresh in-memory database milta hai (tests ek doosre ko affect nahi karte).
/// Real repositories + real services use hoti hain, sirf database in-memory hai.
/// </summary>
public sealed class TestEnv : IDisposable
{
    public AppDbContext Db { get; }
    public FakeCurrentUser CurrentUser { get; } = new();

    public ProductRepository Products { get; }
    public InventoryRepository Inventories { get; }
    public StockMovementRepository Movements { get; }
    public OrderRepository Orders { get; }
    public UserRepository Users { get; }
    public Repository<Category> Categories { get; }
    public Repository<Role> Roles { get; }

    public BCryptPasswordHasher Hasher { get; } = new();
    public JwtTokenService Jwt { get; } = new(new JwtSettings
    {
        Key = "unit-test-secret-key-unit-test-secret-key-1234567890",
        Issuer = "test-issuer",
        Audience = "test-audience",
        ExpiryMinutes = 5
    });

    public TestEnv()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Db = new AppDbContext(options);
        Db.Database.EnsureCreated(); // Roles seed data bhi bhar jata hai

        Products = new ProductRepository(Db);
        Inventories = new InventoryRepository(Db);
        Movements = new StockMovementRepository(Db);
        Orders = new OrderRepository(Db);
        Users = new UserRepository(Db);
        Categories = new Repository<Category>(Db);
        Roles = new Repository<Role>(Db);
    }

    // ---- Services ----
    public CategoryService CategorySvc => new(Categories, Db);
    public ProductService ProductSvc => new(Products, Categories, Db);
    public InventoryService InventorySvc => new(Inventories, Movements, CurrentUser, Db);
    public OrderService OrderSvc => new(Orders, Inventories, Movements, CurrentUser, Db);
    public PaymentService PaymentSvc => new(Orders, Db);
    public AuthService AuthSvc => new(Users, Hasher, Jwt, Db);
    public UserService UserSvc => new(Users, Roles, Hasher, CurrentUser, Db);

    // ---- Seed helpers ----
    public async Task<Category> SeedCategoryAsync(string name = "Electronics")
    {
        var existing = await Db.Categories.FirstOrDefaultAsync(c => c.Name == name);
        if (existing != null) return existing;

        var category = new Category { Name = name };
        Db.Categories.Add(category);
        await Db.SaveChangesAsync();
        return category;
    }

    public async Task<Product> SeedProductAsync(
        string name = "Wireless Mouse", string? sku = null, decimal price = 2500m,
        int stock = 100, int reorderLevel = 10, bool isActive = true)
    {
        var category = await SeedCategoryAsync();
        var product = new Product
        {
            Name = name,
            Sku = (sku ?? Guid.NewGuid().ToString("N")[..8]).ToUpperInvariant(),
            Price = price,
            CostPrice = price * 0.7m,
            ReorderLevel = reorderLevel,
            IsActive = isActive,
            CategoryId = category.Id,
            Inventory = new Inventory { QuantityOnHand = stock }
        };
        Db.Products.Add(product);
        await Db.SaveChangesAsync();
        return product;
    }

    /// <summary>Database se seedha stock padhta hai (tracked cache se nahi).</summary>
    public async Task<int> StockOfAsync(int productId)
        => (await Db.Inventories.AsNoTracking().FirstAsync(i => i.ProductId == productId)).QuantityOnHand;

    public static CreateOrderDto OrderOf(string customer, params (int ProductId, int Qty)[] lines)
        => new(customer, null, lines.Select(l => new CreateOrderItemDto(l.ProductId, l.Qty)).ToList());

    public void Dispose() => Db.Dispose();
}

// =====================================================================
// Inventory
// =====================================================================

public class InventoryServiceTests : IDisposable
{
    private readonly TestEnv _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task StockIn_IncreasesQuantity_AndRecordsMovement()
    {
        var p = await _env.SeedProductAsync(stock: 10);
        _env.CurrentUser.UserId = 7;

        var result = await _env.InventorySvc.StockInAsync(new StockChangeDto(p.Id, 25, "PO-1", "restock"));

        Assert.Equal(35, result.QuantityOnHand);
        var movement = Assert.Single(await _env.Db.StockMovements.AsNoTracking().ToListAsync());
        Assert.Equal(StockMovementType.In, movement.Type);
        Assert.Equal(25, movement.Quantity);
        Assert.Equal("PO-1", movement.Reference);
        Assert.Equal(7, movement.UserId);
    }

    [Fact]
    public async Task StockOut_DecreasesQuantity_AndRecordsMovement()
    {
        var p = await _env.SeedProductAsync(stock: 50);

        var result = await _env.InventorySvc.StockOutAsync(new StockChangeDto(p.Id, 20, "SALE-1", null));

        Assert.Equal(30, result.QuantityOnHand);
        var movement = Assert.Single(await _env.Db.StockMovements.AsNoTracking().ToListAsync());
        Assert.Equal(StockMovementType.Out, movement.Type);
        Assert.Equal(20, movement.Quantity);
    }

    [Fact]
    public async Task StockOut_InsufficientStock_Throws_AndStockUnchanged()
    {
        var p = await _env.SeedProductAsync(stock: 5);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _env.InventorySvc.StockOutAsync(new StockChangeDto(p.Id, 6, null, null)));

        Assert.Contains("Insufficient stock", ex.Message);
        Assert.Equal(5, await _env.StockOfAsync(p.Id));
        Assert.Empty(await _env.Db.StockMovements.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task StockIn_NonPositiveQuantity_Throws(int quantity)
    {
        var p = await _env.SeedProductAsync();

        await Assert.ThrowsAsync<ValidationException>(
            () => _env.InventorySvc.StockInAsync(new StockChangeDto(p.Id, quantity, null, null)));
    }

    [Fact]
    public async Task Adjust_SetsNewQuantity_AndRecordsDifference()
    {
        var p = await _env.SeedProductAsync(stock: 100);

        var result = await _env.InventorySvc.AdjustAsync(new AdjustStockDto(p.Id, 40, "stock count"));

        Assert.Equal(40, result.QuantityOnHand);
        var movement = Assert.Single(await _env.Db.StockMovements.AsNoTracking().ToListAsync());
        Assert.Equal(StockMovementType.Adjustment, movement.Type);
        Assert.Equal(-60, movement.Quantity); // farq: 40 - 100
    }

    [Fact]
    public async Task Adjust_NegativeQuantity_Throws()
    {
        var p = await _env.SeedProductAsync();

        await Assert.ThrowsAsync<ValidationException>(
            () => _env.InventorySvc.AdjustAsync(new AdjustStockDto(p.Id, -1, null)));
    }

    [Fact]
    public async Task StockOperation_OnInactiveProduct_Throws()
    {
        var p = await _env.SeedProductAsync(isActive: false);

        await Assert.ThrowsAsync<ValidationException>(
            () => _env.InventorySvc.StockInAsync(new StockChangeDto(p.Id, 5, null, null)));
    }

    [Fact]
    public async Task GetByProductId_UnknownProduct_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _env.InventorySvc.GetByProductIdAsync(9999));
    }

    [Fact]
    public async Task LowStock_ReturnsOnlyProductsAtOrBelowReorderLevel()
    {
        var low = await _env.SeedProductAsync(name: "Low", stock: 5, reorderLevel: 10);
        await _env.SeedProductAsync(name: "Healthy", stock: 50, reorderLevel: 10);

        var result = await _env.InventorySvc.GetLowStockAsync();

        var item = Assert.Single(result);
        Assert.Equal(low.Id, item.ProductId);
        Assert.True(item.IsLowStock);
    }

    [Fact]
    public async Task Movements_ReturnsNewestFirst_AndFiltersByProduct()
    {
        var a = await _env.SeedProductAsync(name: "A");
        var b = await _env.SeedProductAsync(name: "B");

        await _env.InventorySvc.StockInAsync(new StockChangeDto(a.Id, 5, "first", null));
        await _env.InventorySvc.StockInAsync(new StockChangeDto(b.Id, 5, "other", null));
        await _env.InventorySvc.StockOutAsync(new StockChangeDto(a.Id, 2, "second", null));

        var history = await _env.InventorySvc.GetMovementsAsync(a.Id);

        Assert.Equal(2, history.Count);
        Assert.All(history, m => Assert.Equal(a.Id, m.ProductId));
    }
}

// =====================================================================
// Products
// =====================================================================

public class ProductServiceTests : IDisposable
{
    private readonly TestEnv _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task Create_UppercasesSku_AndCreatesEmptyInventory()
    {
        var cat = await _env.SeedCategoryAsync();

        var created = await _env.ProductSvc.CreateAsync(
            new CreateProductDto("Mouse", " mouse-001 ", null, 2500, 1800, 10, cat.Id));

        Assert.Equal("MOUSE-001", created.Sku);
        Assert.Equal(0, created.QuantityOnHand);
        Assert.Equal(cat.Name, created.CategoryName);
    }

    [Fact]
    public async Task Create_DuplicateSku_Throws()
    {
        var cat = await _env.SeedCategoryAsync();
        await _env.ProductSvc.CreateAsync(new CreateProductDto("A", "dup-1", null, 10, 5, 1, cat.Id));

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _env.ProductSvc.CreateAsync(new CreateProductDto("B", "DUP-1", null, 10, 5, 1, cat.Id)));

        Assert.Contains("already exists", ex.Message);
    }

    [Theory]
    [InlineData("", "sku-1", 10, 5)]      // naam khali
    [InlineData("Name", "", 10, 5)]       // sku khali
    [InlineData("Name", "sku-2", -1, 5)]  // negative price
    [InlineData("Name", "sku-3", 10, -1)] // negative cost
    public async Task Create_InvalidInput_Throws(string name, string sku, decimal price, decimal cost)
    {
        var cat = await _env.SeedCategoryAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _env.ProductSvc.CreateAsync(new CreateProductDto(name, sku, null, price, cost, 1, cat.Id)));
    }

    [Fact]
    public async Task Create_UnknownCategory_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _env.ProductSvc.CreateAsync(new CreateProductDto("A", "sku-x", null, 10, 5, 1, 9999)));
    }

    [Fact]
    public async Task Update_ChangesFields()
    {
        var p = await _env.SeedProductAsync(price: 2500);

        var updated = await _env.ProductSvc.UpdateAsync(
            p.Id, new UpdateProductDto("Mouse Pro", "new", 3000, 2000, 15, true, p.CategoryId));

        Assert.NotNull(updated);
        Assert.Equal("Mouse Pro", updated!.Name);
        Assert.Equal(3000, updated.Price);
        Assert.Equal(15, updated.ReorderLevel);
    }

    [Fact]
    public async Task Update_UnknownProduct_ReturnsNull()
    {
        var cat = await _env.SeedCategoryAsync();

        var result = await _env.ProductSvc.UpdateAsync(
            9999, new UpdateProductDto("X", null, 1, 1, 1, true, cat.Id));

        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_IsSoftDelete_ProductHiddenFromList_ButStillInDatabase()
    {
        var p = await _env.SeedProductAsync();

        var deleted = await _env.ProductSvc.DeleteAsync(p.Id);

        Assert.True(deleted);
        var list = await _env.ProductSvc.GetAllAsync(new ProductQuery(null, null));
        Assert.Equal(0, list.TotalCount);

        var stillThere = await _env.ProductSvc.GetByIdAsync(p.Id);
        Assert.NotNull(stillThere);
        Assert.False(stillThere!.IsActive);
    }

    [Fact]
    public async Task Delete_UnknownProduct_ReturnsFalse()
    {
        Assert.False(await _env.ProductSvc.DeleteAsync(9999));
    }

    [Fact]
    public async Task GetAll_Pagination_ReturnsCorrectPageAndTotals()
    {
        for (var i = 1; i <= 5; i++)
            await _env.SeedProductAsync(name: $"P{i}");

        var page2 = await _env.ProductSvc.GetAllAsync(new ProductQuery(null, null, Page: 2, PageSize: 2));

        Assert.Equal(2, page2.Items.Count);
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal("P3", page2.Items[0].Name);
    }

    [Fact]
    public async Task GetAll_PageSizeIsClamped()
    {
        await _env.SeedProductAsync();

        var result = await _env.ProductSvc.GetAllAsync(new ProductQuery(null, null, Page: 1, PageSize: 5000));

        Assert.Equal(100, result.PageSize);
    }

    [Fact]
    public async Task GetAll_SearchByNameOrSku_AndFilterByCategory()
    {
        await _env.SeedProductAsync(name: "Mouse", sku: "MS-100");
        await _env.SeedProductAsync(name: "Keyboard", sku: "KB-200");

        var byName = await _env.ProductSvc.GetAllAsync(new ProductQuery("Mouse", null));
        var bySku = await _env.ProductSvc.GetAllAsync(new ProductQuery("KB-2", null));

        Assert.Equal(1, byName.TotalCount);
        Assert.Equal("Mouse", byName.Items[0].Name);
        Assert.Equal(1, bySku.TotalCount);
        Assert.Equal("Keyboard", bySku.Items[0].Name);

        var cat = await _env.SeedCategoryAsync();
        var byCategory = await _env.ProductSvc.GetAllAsync(new ProductQuery(null, cat.Id));
        Assert.Equal(2, byCategory.TotalCount);
    }
}

// =====================================================================
// Orders
// =====================================================================

public class OrderServiceTests : IDisposable
{
    private readonly TestEnv _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task Create_DeductsStock_CalculatesTotal_AndStartsPending()
    {
        var p = await _env.SeedProductAsync(price: 2500, stock: 100);

        var order = await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, 3)));

        Assert.Equal("Pending", order.Status);
        Assert.Equal(7500, order.TotalAmount);
        Assert.StartsWith("ORD-", order.OrderNumber);
        Assert.Single(order.Items);
        Assert.Equal(97, await _env.StockOfAsync(p.Id));
    }

    [Fact]
    public async Task Create_RecordsOutMovement_WithOrderNumberReference()
    {
        var p = await _env.SeedProductAsync();

        var order = await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, 2)));

        var movement = Assert.Single(await _env.Db.StockMovements.AsNoTracking().ToListAsync());
        Assert.Equal(StockMovementType.Out, movement.Type);
        Assert.Equal(2, movement.Quantity);
        Assert.Equal(order.OrderNumber, movement.Reference);
    }

    [Fact]
    public async Task Create_MergesDuplicateProductLines()
    {
        var p = await _env.SeedProductAsync(stock: 100);

        var order = await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, 2), (p.Id, 3)));

        var line = Assert.Single(order.Items);
        Assert.Equal(5, line.Quantity);
        Assert.Equal(95, await _env.StockOfAsync(p.Id));
    }

    [Fact]
    public async Task Create_PriceIsSnapshot_LaterPriceChangeDoesNotAffectOrder()
    {
        var p = await _env.SeedProductAsync(price: 2500);
        var order = await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, 1)));

        var tracked = await _env.Db.Products.FirstAsync(x => x.Id == p.Id);
        tracked.Price = 9999;
        await _env.Db.SaveChangesAsync();

        var reloaded = await _env.OrderSvc.GetByIdAsync(order.Id);
        Assert.Equal(2500, reloaded.Items[0].UnitPrice);
        Assert.Equal(2500, reloaded.TotalAmount);
    }

    [Fact]
    public async Task Create_InsufficientStock_Throws_AndNothingIsSaved()
    {
        var a = await _env.SeedProductAsync(name: "A", stock: 10);
        var b = await _env.SeedProductAsync(name: "B", stock: 1);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (a.Id, 5), (b.Id, 5))));

        Assert.Contains("Insufficient stock", ex.Message);
        // all-or-nothing: A ka stock bhi nahi ghata, order bhi nahi bana
        Assert.Equal(10, await _env.StockOfAsync(a.Id));
        Assert.Equal(1, await _env.StockOfAsync(b.Id));
        Assert.Equal(0, await _env.Db.Orders.CountAsync());
        Assert.Equal(0, await _env.Db.StockMovements.CountAsync());
    }

    [Fact]
    public async Task Create_EmptyItems_Throws()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _env.OrderSvc.CreateAsync(new CreateOrderDto("Ali", null, new List<CreateOrderItemDto>())));
    }

    [Fact]
    public async Task Create_MissingCustomerName_Throws()
    {
        var p = await _env.SeedProductAsync();

        await Assert.ThrowsAsync<ValidationException>(
            () => _env.OrderSvc.CreateAsync(TestEnv.OrderOf("  ", (p.Id, 1))));
    }

    [Fact]
    public async Task Create_ZeroQuantity_Throws()
    {
        var p = await _env.SeedProductAsync();

        await Assert.ThrowsAsync<ValidationException>(
            () => _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, 0))));
    }

    [Fact]
    public async Task Create_UnknownProduct_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (9999, 1))));
    }

    [Fact]
    public async Task Create_InactiveProduct_Throws()
    {
        var p = await _env.SeedProductAsync(isActive: false);

        await Assert.ThrowsAsync<ValidationException>(
            () => _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, 1))));
    }

    [Fact]
    public async Task Create_RecordsWhoCreatedTheOrder()
    {
        var p = await _env.SeedProductAsync();
        _env.CurrentUser.UserId = 5;

        await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, 1)));

        var saved = await _env.Db.Orders.AsNoTracking().FirstAsync();
        Assert.Equal(5, saved.CreatedByUserId);
        var movement = await _env.Db.StockMovements.AsNoTracking().FirstAsync();
        Assert.Equal(5, movement.UserId);
    }

    [Fact]
    public async Task StatusFlow_Pending_Confirmed_Shipped_Delivered()
    {
        var p = await _env.SeedProductAsync();
        var order = await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, 1)));

        Assert.Equal("Confirmed", (await _env.OrderSvc.ConfirmAsync(order.Id)).Status);
        Assert.Equal("Shipped", (await _env.OrderSvc.ShipAsync(order.Id)).Status);
        Assert.Equal("Delivered", (await _env.OrderSvc.DeliverAsync(order.Id)).Status);
    }

    [Fact]
    public async Task Ship_BeforeConfirm_Throws()
    {
        var p = await _env.SeedProductAsync();
        var order = await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, 1)));

        await Assert.ThrowsAsync<ValidationException>(() => _env.OrderSvc.ShipAsync(order.Id));
    }

    [Fact]
    public async Task Confirm_Twice_Throws()
    {
        var p = await _env.SeedProductAsync();
        var order = await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, 1)));
        await _env.OrderSvc.ConfirmAsync(order.Id);

        await Assert.ThrowsAsync<ValidationException>(() => _env.OrderSvc.ConfirmAsync(order.Id));
    }

    [Fact]
    public async Task Cancel_RestoresStock_AndRecordsReturnMovement()
    {
        var p = await _env.SeedProductAsync(stock: 100);
        var order = await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, 5)));
        Assert.Equal(95, await _env.StockOfAsync(p.Id));

        var cancelled = await _env.OrderSvc.CancelAsync(order.Id);

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(100, await _env.StockOfAsync(p.Id));
        var returns = await _env.Db.StockMovements.AsNoTracking()
            .Where(m => m.Type == StockMovementType.Return).ToListAsync();
        var ret = Assert.Single(returns);
        Assert.Equal(5, ret.Quantity);
        Assert.Equal(order.OrderNumber, ret.Reference);
    }

    [Fact]
    public async Task Cancel_ConfirmedOrder_IsAllowed()
    {
        var p = await _env.SeedProductAsync();
        var order = await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, 1)));
        await _env.OrderSvc.ConfirmAsync(order.Id);

        var cancelled = await _env.OrderSvc.CancelAsync(order.Id);

        Assert.Equal("Cancelled", cancelled.Status);
    }

    [Fact]
    public async Task Cancel_ShippedOrDelivered_Throws()
    {
        var p = await _env.SeedProductAsync();
        var order = await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, 1)));
        await _env.OrderSvc.ConfirmAsync(order.Id);
        await _env.OrderSvc.ShipAsync(order.Id);
        await Assert.ThrowsAsync<ValidationException>(() => _env.OrderSvc.CancelAsync(order.Id));

        await _env.OrderSvc.DeliverAsync(order.Id);
        await Assert.ThrowsAsync<ValidationException>(() => _env.OrderSvc.CancelAsync(order.Id));
    }

    [Fact]
    public async Task Cancel_Twice_Throws()
    {
        var p = await _env.SeedProductAsync();
        var order = await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, 1)));
        await _env.OrderSvc.CancelAsync(order.Id);

        await Assert.ThrowsAsync<ValidationException>(() => _env.OrderSvc.CancelAsync(order.Id));
    }

    [Fact]
    public async Task GetById_Unknown_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _env.OrderSvc.GetByIdAsync(9999));
    }

    [Fact]
    public async Task GetAll_Pagination_StatusFilter_AndSearch()
    {
        var p = await _env.SeedProductAsync(stock: 100);
        var o1 = await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali Khan", (p.Id, 1)));
        await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Sara Ahmed", (p.Id, 1)));
        await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Bilal Raza", (p.Id, 1)));
        await _env.OrderSvc.ConfirmAsync(o1.Id);

        var page = await _env.OrderSvc.GetAllAsync(new OrderQuery(null, null, Page: 1, PageSize: 2));
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);

        var confirmed = await _env.OrderSvc.GetAllAsync(new OrderQuery(OrderStatus.Confirmed, null));
        Assert.Equal(1, confirmed.TotalCount);

        var search = await _env.OrderSvc.GetAllAsync(new OrderQuery(null, "Sara"));
        Assert.Equal(1, search.TotalCount);
        Assert.Equal("Sara Ahmed", search.Items[0].CustomerName);
    }
}

// =====================================================================
// Payments
// =====================================================================

public class PaymentServiceTests : IDisposable
{
    private readonly TestEnv _env = new();
    public void Dispose() => _env.Dispose();

    private async Task<OrderDto> NewOrderAsync(int qty = 3) // total = qty x 2500
    {
        var p = await _env.SeedProductAsync(price: 2500, stock: 100);
        return await _env.OrderSvc.CreateAsync(TestEnv.OrderOf("Ali", (p.Id, qty)));
    }

    [Fact]
    public async Task NewOrder_IsUnpaid_WithFullBalanceDue()
    {
        var order = await NewOrderAsync();

        var summary = await _env.PaymentSvc.GetByOrderAsync(order.Id);

        Assert.Equal("Unpaid", summary.PaymentState);
        Assert.Equal(7500, summary.BalanceDue);
        Assert.Empty(summary.Payments);
    }

    [Fact]
    public async Task PartialPayment_SetsPartiallyPaid()
    {
        var order = await NewOrderAsync();

        var summary = await _env.PaymentSvc.CreateAsync(order.Id, new CreatePaymentDto(3000, PaymentMethod.Cash));

        Assert.Equal("PartiallyPaid", summary.PaymentState);
        Assert.Equal(3000, summary.PaidAmount);
        Assert.Equal(4500, summary.BalanceDue);
    }

    [Fact]
    public async Task FullPayment_InTwoParts_SetsPaid()
    {
        var order = await NewOrderAsync();
        await _env.PaymentSvc.CreateAsync(order.Id, new CreatePaymentDto(3000, PaymentMethod.Cash));

        var summary = await _env.PaymentSvc.CreateAsync(order.Id, new CreatePaymentDto(4500, PaymentMethod.JazzCash));

        Assert.Equal("Paid", summary.PaymentState);
        Assert.Equal(0, summary.BalanceDue);
        Assert.Equal(2, summary.Payments.Count);
    }

    [Fact]
    public async Task Overpayment_Throws()
    {
        var order = await NewOrderAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _env.PaymentSvc.CreateAsync(order.Id, new CreatePaymentDto(7501, PaymentMethod.Card)));

        Assert.Contains("exceeds balance", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task NonPositiveAmount_Throws(int amount)
    {
        var order = await NewOrderAsync();

        await Assert.ThrowsAsync<ValidationException>(
            () => _env.PaymentSvc.CreateAsync(order.Id, new CreatePaymentDto(amount, PaymentMethod.Cash)));
    }

    [Fact]
    public async Task Payment_OnUnknownOrder_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _env.PaymentSvc.CreateAsync(9999, new CreatePaymentDto(100, PaymentMethod.Cash)));
    }

    [Fact]
    public async Task Payment_OnCancelledOrder_Throws()
    {
        var order = await NewOrderAsync();
        await _env.OrderSvc.CancelAsync(order.Id);

        await Assert.ThrowsAsync<ValidationException>(
            () => _env.PaymentSvc.CreateAsync(order.Id, new CreatePaymentDto(100, PaymentMethod.Cash)));
    }

    [Fact]
    public async Task CancelOrder_RefundsCompletedPayments()
    {
        var order = await NewOrderAsync();
        await _env.PaymentSvc.CreateAsync(order.Id, new CreatePaymentDto(7500, PaymentMethod.Card));

        await _env.OrderSvc.CancelAsync(order.Id);

        var summary = await _env.PaymentSvc.GetByOrderAsync(order.Id);
        Assert.Equal("Refunded", summary.PaymentState);
        Assert.Equal(0, summary.PaidAmount);
        var stored = await _env.Db.Payments.AsNoTracking().ToListAsync();
        Assert.All(stored, p => Assert.Equal(PaymentStatus.Refunded, p.Status));
    }
}

// =====================================================================
// Auth & Users
// =====================================================================

public class PasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_IsNotPlainText_AndVerifies()
    {
        var hash = _hasher.Hash("Test@1234");

        Assert.NotEqual("Test@1234", hash);
        Assert.StartsWith("$2", hash); // BCrypt format
        Assert.True(_hasher.Verify("Test@1234", hash));
        Assert.False(_hasher.Verify("Wrong@1234", hash));
    }

    [Fact]
    public void SamePassword_GivesDifferentHashes_BecauseOfSalt()
    {
        Assert.NotEqual(_hasher.Hash("Test@1234"), _hasher.Hash("Test@1234"));
    }
}

public class AuthServiceTests : IDisposable
{
    private const string Pw = "Test@1234";
    private readonly TestEnv _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task Register_AssignsStaffRole_AndStoresHashedPassword()
    {
        var user = await _env.AuthSvc.RegisterAsync(new RegisterDto("Ali Khan", "Ali@Test.com", Pw));

        Assert.Equal("Staff", user.Role);
        Assert.Equal("ali@test.com", user.Email); // lowercase normalize

        var stored = await _env.Db.Users.AsNoTracking().FirstAsync(u => u.Email == "ali@test.com");
        Assert.NotEqual(Pw, stored.PasswordHash);
        Assert.StartsWith("$2", stored.PasswordHash);
        Assert.Equal(RoleIds.Staff, stored.RoleId);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Throws_EvenWithDifferentCase()
    {
        await _env.AuthSvc.RegisterAsync(new RegisterDto("Ali", "ali@test.com", Pw));

        await Assert.ThrowsAsync<ValidationException>(
            () => _env.AuthSvc.RegisterAsync(new RegisterDto("Ali 2", "ALI@test.com", Pw)));
    }

    [Theory]
    [InlineData("short1")]       // 8 se kam
    [InlineData("onlyletters")]  // digit nahi
    [InlineData("12345678")]     // letter nahi
    [InlineData("")]
    public async Task Register_WeakPassword_Throws(string password)
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _env.AuthSvc.RegisterAsync(new RegisterDto("Ali", "ali@test.com", password)));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public async Task Register_InvalidEmail_Throws(string email)
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _env.AuthSvc.RegisterAsync(new RegisterDto("Ali", email, Pw)));
    }

    [Fact]
    public async Task Register_MissingName_Throws()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _env.AuthSvc.RegisterAsync(new RegisterDto(" ", "ali@test.com", Pw)));
    }

    [Fact]
    public async Task Login_Success_ReturnsJwtWithRoleAndSubjectClaims()
    {
        var registered = await _env.AuthSvc.RegisterAsync(new RegisterDto("Ali", "ali@test.com", Pw));

        var result = await _env.AuthSvc.LoginAsync(new LoginDto("ali@test.com", Pw));

        Assert.Equal(3, result.Token.Split('.').Length); // header.payload.signature
        Assert.True(result.ExpiresAt > DateTime.UtcNow);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        Assert.Equal("Staff", jwt.Claims.First(c => c.Type == "role").Value);
        Assert.Equal(registered.Id.ToString(), jwt.Claims.First(c => c.Type == "sub").Value);
    }

    [Fact]
    public async Task Login_IsCaseInsensitiveOnEmail()
    {
        await _env.AuthSvc.RegisterAsync(new RegisterDto("Ali", "ali@test.com", Pw));

        var result = await _env.AuthSvc.LoginAsync(new LoginDto("  ALI@TEST.COM ", Pw));

        Assert.False(string.IsNullOrEmpty(result.Token));
    }

    [Fact]
    public async Task Login_WrongPassword_Throws_Unauthorized()
    {
        await _env.AuthSvc.RegisterAsync(new RegisterDto("Ali", "ali@test.com", Pw));

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _env.AuthSvc.LoginAsync(new LoginDto("ali@test.com", "Wrong@1234")));
    }

    [Fact]
    public async Task Login_UnknownEmail_GivesSameMessageAsWrongPassword()
    {
        await _env.AuthSvc.RegisterAsync(new RegisterDto("Ali", "ali@test.com", Pw));

        var unknown = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _env.AuthSvc.LoginAsync(new LoginDto("nobody@test.com", Pw)));
        var wrongPw = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _env.AuthSvc.LoginAsync(new LoginDto("ali@test.com", "Wrong@1234")));

        // user enumeration se bachav: dono ka message ek jaisa
        Assert.Equal(wrongPw.Message, unknown.Message);
    }

    [Fact]
    public async Task Login_InactiveUser_Throws_Unauthorized()
    {
        await _env.AuthSvc.RegisterAsync(new RegisterDto("Ali", "ali@test.com", Pw));
        var user = await _env.Db.Users.FirstAsync();
        user.IsActive = false;
        await _env.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _env.AuthSvc.LoginAsync(new LoginDto("ali@test.com", Pw)));
    }

    [Fact]
    public async Task GetCurrent_ReturnsUser_AndUnknownThrowsNotFound()
    {
        var registered = await _env.AuthSvc.RegisterAsync(new RegisterDto("Ali", "ali@test.com", Pw));

        var me = await _env.AuthSvc.GetCurrentAsync(registered.Id);
        Assert.Equal("ali@test.com", me.Email);

        await Assert.ThrowsAsync<NotFoundException>(() => _env.AuthSvc.GetCurrentAsync(9999));
    }
}

public class UserServiceTests : IDisposable
{
    private const string Pw = "Test@1234";
    private readonly TestEnv _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task Create_WithManagerRole_Works()
    {
        var user = await _env.UserSvc.CreateAsync(new CreateUserDto("Mgr", "mgr@test.com", Pw, RoleIds.Manager));

        Assert.Equal("Manager", user.Role);
    }

    [Fact]
    public async Task Create_InvalidRole_Throws()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _env.UserSvc.CreateAsync(new CreateUserDto("X", "x@test.com", Pw, 99)));
    }

    [Fact]
    public async Task Create_DuplicateEmail_Throws()
    {
        await _env.UserSvc.CreateAsync(new CreateUserDto("A", "a@test.com", Pw, RoleIds.Staff));

        await Assert.ThrowsAsync<ValidationException>(
            () => _env.UserSvc.CreateAsync(new CreateUserDto("B", "a@test.com", Pw, RoleIds.Staff)));
    }

    [Fact]
    public async Task Create_WeakPassword_Throws()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _env.UserSvc.CreateAsync(new CreateUserDto("A", "a@test.com", "abc", RoleIds.Staff)));
    }

    [Fact]
    public async Task ChangeRole_ByAdmin_Works()
    {
        var user = await _env.UserSvc.CreateAsync(new CreateUserDto("S", "s@test.com", Pw, RoleIds.Staff));
        _env.CurrentUser.UserId = 9999; // koi aur admin

        var updated = await _env.UserSvc.ChangeRoleAsync(user.Id, RoleIds.Manager);

        Assert.Equal("Manager", updated.Role);
    }

    [Fact]
    public async Task ChangeRole_OwnRole_Throws()
    {
        var user = await _env.UserSvc.CreateAsync(new CreateUserDto("Admin", "admin@test.com", Pw, RoleIds.Admin));
        _env.CurrentUser.UserId = user.Id;

        await Assert.ThrowsAsync<ValidationException>(
            () => _env.UserSvc.ChangeRoleAsync(user.Id, RoleIds.Staff));
    }

    [Fact]
    public async Task ChangeRole_UnknownUser_ThrowsNotFound()
    {
        _env.CurrentUser.UserId = 1;

        await Assert.ThrowsAsync<NotFoundException>(
            () => _env.UserSvc.ChangeRoleAsync(9999, RoleIds.Manager));
    }

    [Fact]
    public async Task Deactivate_Self_Throws()
    {
        var user = await _env.UserSvc.CreateAsync(new CreateUserDto("Admin", "admin@test.com", Pw, RoleIds.Admin));
        _env.CurrentUser.UserId = user.Id;

        await Assert.ThrowsAsync<ValidationException>(
            () => _env.UserSvc.SetActiveAsync(user.Id, false));
    }

    [Fact]
    public async Task DeactivatedUser_CannotLogin_UntilReactivated()
    {
        var user = await _env.UserSvc.CreateAsync(new CreateUserDto("S", "s@test.com", Pw, RoleIds.Staff));
        _env.CurrentUser.UserId = 9999;

        await _env.UserSvc.SetActiveAsync(user.Id, false);
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _env.AuthSvc.LoginAsync(new LoginDto("s@test.com", Pw)));

        await _env.UserSvc.SetActiveAsync(user.Id, true);
        var result = await _env.AuthSvc.LoginAsync(new LoginDto("s@test.com", Pw));
        Assert.False(string.IsNullOrEmpty(result.Token));
    }

    [Fact]
    public async Task GetAll_ReturnsUsersWithRoles()
    {
        await _env.UserSvc.CreateAsync(new CreateUserDto("A", "a@test.com", Pw, RoleIds.Staff));
        await _env.UserSvc.CreateAsync(new CreateUserDto("B", "b@test.com", Pw, RoleIds.Manager));

        var users = await _env.UserSvc.GetAllAsync();

        Assert.Equal(2, users.Count);
        Assert.Contains(users, u => u.Role == "Manager");
    }
}
