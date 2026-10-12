using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaterlilyInventory.Web.Domain.Entities;

namespace WaterlilyInventory.Web.Data.Configurations;

/// <summary>
/// EF Core fluent configuration for the <see cref="Product"/> entity.
/// Defines SQL column types, constraints, check constraints, and indexes.
/// Applied by <see cref="ApplicationDbContext.OnModelCreating"/>.
/// </summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        // Table name and check constraints — enforce non-negative values at the database level
        builder.ToTable("Products", t =>
        {
            t.HasCheckConstraint("CK_Products_Price_NonNegative", "[Price] >= 0");
            t.HasCheckConstraint("CK_Products_Stock_NonNegative", "[Stock] >= 0");
        });

        // Primary key — identity column
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .UseIdentityColumn();

        // Name: required, max 150 characters
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(150);

        // Category: required, max 100 characters
        builder.Property(p => p.Category)
            .IsRequired()
            .HasMaxLength(100);

        // Price: decimal(18,2), non-negative check constraint
        builder.Property(p => p.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        // Stock: integer, non-negative check constraint
        builder.Property(p => p.Stock)
            .IsRequired();

        // Index on Category — useful for report grouping queries and filtering
        builder.HasIndex(p => p.Category)
            .HasDatabaseName("IX_Products_Category");
    }
}
