using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WaterlilyInventory.Web.Domain.Entities;

namespace WaterlilyInventory.Web.Data;

/// <summary>
/// Development demo database seeder.
/// Seeds at least twelve products across four categories, covering zero stock,
/// boundary stock levels (9, 10, 11), different prices, and tied highest-value categories.
/// Refuses to overwrite existing data if products already exist in the database.
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    /// Seeds the development database with demo inventory data if the Products table is empty.
    /// </summary>
    /// <param name="context">The scoped database context.</param>
    /// <param name="logger">Optional logger for logging seeder actions.</param>
    /// <param name="cancellationToken">Cancellation token for database operations.</param>
    /// <returns>True if data was seeded; false if existing data was preserved.</returns>
    public static async Task<bool> SeedDemoDataAsync(
        ApplicationDbContext context,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Safety guard: refuse to overwrite existing product data
        if (await context.Products.AnyAsync(cancellationToken))
        {
            logger?.LogInformation("DatabaseSeeder: Products table already contains data. Seeding skipped.");
            return false;
        }

        var demoProducts = new List<Product>
        {
            // Category: Electronics (Valuation: $3,000.00, Units: 110, Avg Price: $31.67)
            new() { Name = "Laptop Stand", Category = "Electronics", Price = 50.00m, Stock = 20 },
            new() { Name = "USB-C Cable", Category = "Electronics", Price = 20.00m, Stock = 50 },
            new() { Name = "Wireless Mouse", Category = "Electronics", Price = 25.00m, Stock = 40 },

            // Category: Office Furniture (Valuation: $3,000.00, Units: 30, Avg Price: $100.00)
            // Includes boundary stock levels 10, 11, and 9
            new() { Name = "Ergonomic Chair", Category = "Office Furniture", Price = 200.00m, Stock = 10 },
            new() { Name = "Desk Lamp", Category = "Office Furniture", Price = 50.00m, Stock = 11 },
            new() { Name = "Footrest", Category = "Office Furniture", Price = 50.00m, Stock = 9 },

            // Category: Stationery (Valuation: $1,500.00, Units: 130, Avg Price: $17.00)
            // Includes zero-stock item
            new() { Name = "Notebook Set", Category = "Stationery", Price = 15.00m, Stock = 0 },
            new() { Name = "Premium Pen", Category = "Stationery", Price = 30.00m, Stock = 30 },
            new() { Name = "Sticky Notes Pack", Category = "Stationery", Price = 6.00m, Stock = 100 },

            // Category: Accessories (Valuation: $500.00, Units: 40, Avg Price: $47.50)
            // Includes boundary stock 10 and zero-stock expensive item
            new() { Name = "Desk Mat", Category = "Accessories", Price = 25.00m, Stock = 10 },
            new() { Name = "Cable Organizer", Category = "Accessories", Price = 10.00m, Stock = 20 },
            new() { Name = "Monitor Wipe", Category = "Accessories", Price = 5.00m, Stock = 10 },
            new() { Name = "Mechanical Keyboard", Category = "Accessories", Price = 150.00m, Stock = 0 }
        };

        await context.Products.AddRangeAsync(demoProducts, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger?.LogInformation("DatabaseSeeder: Successfully seeded {Count} demo products.", demoProducts.Count);
        return true;
    }
}
