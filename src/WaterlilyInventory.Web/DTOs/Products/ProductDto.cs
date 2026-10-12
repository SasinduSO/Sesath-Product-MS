namespace WaterlilyInventory.Web.DTOs.Products;

/// <summary>
/// Immutable output DTO for product data returned to the browser.
/// Used for caching — callers cannot mutate shared cached values.
/// Does not expose tracked EF entities across the boundary.
/// </summary>
/// <param name="Id">Product identity key.</param>
/// <param name="Name">Product name.</param>
/// <param name="Category">Product category (plain string).</param>
/// <param name="Price">Product price in the configured display currency.</param>
/// <param name="Stock">Current stock quantity.</param>
public sealed record ProductDto(
    int Id,
    string Name,
    string Category,
    decimal Price,
    int Stock
);
