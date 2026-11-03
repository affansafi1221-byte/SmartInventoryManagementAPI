## Getting Started
1. Install .NET 10 SDK and SQL Server LocalDB (included with Visual Studio)
2. `dotnet tool install --global dotnet-ef`
3. `dotnet ef database update --project src/SmartInventory.Infrastructure --startup-project .`
4. `dotnet run`, then open `https://localhost:7014/swagger`

## Architecture
Clean Architecture: Domain, Application, Infrastructure, API.
