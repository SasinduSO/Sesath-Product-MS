namespace WaterlilyInventory.Web.DTOs.Products;

/// <summary>
/// Data transfer object summarizing the completion of a bulk product import.
/// </summary>
public record BulkProductImportResultDto(
    int ImportedCount,
    IReadOnlyList<int> CreatedIds
);
