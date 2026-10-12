using System.ComponentModel.DataAnnotations;

namespace WaterlilyInventory.Web.Domain.Entities;

/// <summary>
/// Product entity representing an inventory item.
/// SQL column types, constraints, and indexes are configured in
/// <see cref="Data.Configurations.ProductConfiguration"/> — not here.
/// Business validation is handled in the Service layer, not in entity property setters.
/// </summary>
public class Product : IEntity
{
    /// <summary>
    /// Identity primary key assigned by SQL Server on insert.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Product name. Required, max 150 characters, must be nonblank after trimming.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Product category as a plain string — no Category table.
    /// Required, max 100 characters, must be nonblank after trimming.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Product price in the configured display currency. Decimal(18,2), must be ≥ 0.
    /// Excessive precision is rejected at the Service boundary, not silently rounded.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Number of units in stock. Must be ≥ 0.
    /// </summary>
    public int Stock { get; set; }
}
