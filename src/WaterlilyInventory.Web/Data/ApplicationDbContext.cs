using Microsoft.EntityFrameworkCore;
using WaterlilyInventory.Web.Data.Configurations;
using WaterlilyInventory.Web.Domain.Entities;

namespace WaterlilyInventory.Web.Data;

/// <summary>
/// Application database context for EF Core.
/// Registered with a scoped lifetime via dependency injection in Program.cs,
/// using the SQL Server provider with the "DefaultConnection" connection string.
/// Does not contain business workflows — those belong in the Service layer.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Products table. The only entity in this application — Category is a string column, not a separate table.
    /// </summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>
    /// Applies entity configurations from the Data/Configurations folder.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply ProductConfiguration (IEntityTypeConfiguration<Product>)
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
    }
}
