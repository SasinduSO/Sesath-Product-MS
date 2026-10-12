namespace WaterlilyInventory.Web.Caching;

/// <summary>
/// Centralized, typed/namespaced cache keys for product and report data.
/// Provides consistent key generation and prefix-based invalidation scopes.
/// </summary>
public static class CacheKeys
{
    /// <summary>Prefix for all product-related cache entries.</summary>
    public const string ProductPrefix = "product:";

    /// <summary>Prefix for all report-related cache entries.</summary>
    public const string ReportPrefix = "report:";

    /// <summary>Cache key for the full product list.</summary>
    public static string ProductList => $"{ProductPrefix}list";

    /// <summary>Cache key for an individual product by Id.</summary>
    public static string ProductById(int id) => $"{ProductPrefix}id:{id}";

    /// <summary>Cache key for the average-price-by-category report.</summary>
    public static string ReportAveragePrice => $"{ReportPrefix}avg-price";

    /// <summary>Cache key for the highest-stock-value-category report.</summary>
    public static string ReportHighestValue => $"{ReportPrefix}highest-value";

    /// <summary>Cache key for the inventory-valuation-by-category report.</summary>
    public static string ReportValuation => $"{ReportPrefix}valuation";

    /// <summary>Cache key for the low-stock report with a specific threshold.</summary>
    public static string ReportLowStock(int threshold) => $"{ReportPrefix}low-stock:{threshold}";

    /// <summary>Cache key for the top-five-most-expensive report.</summary>
    public static string ReportTopExpensive => $"{ReportPrefix}top-expensive";
}
