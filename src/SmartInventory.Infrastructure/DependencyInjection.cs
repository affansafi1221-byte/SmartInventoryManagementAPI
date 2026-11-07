using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartInventory.Application.Interfaces;
using SmartInventory.Application.Services;
using SmartInventory.Infrastructure.Data;
using SmartInventory.Infrastructure.Options;
using SmartInventory.Infrastructure.Repositories;
using SmartInventory.Infrastructure.Services;

namespace SmartInventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlServer(config.GetConnectionString("DefaultConnection")));

        // Repositories
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Services
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        // JWT + password hashing
        var jwt = new JwtSettings
        {
            Key = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured."),
            Issuer = config["Jwt:Issuer"] ?? "SmartInventoryAPI",
            Audience = config["Jwt:Audience"] ?? "SmartInventoryClient",
            ExpiryMinutes = int.TryParse(config["Jwt:ExpiryMinutes"], out var minutes) ? minutes : 60
        };
        services.AddSingleton(jwt);
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        return services;
    }
}