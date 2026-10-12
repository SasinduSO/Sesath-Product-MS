namespace WaterlilyInventory.Web.DTOs.Products;

/// <summary>
/// Data transfer object representing the validation preview result of a bulk product upload.
/// Segregates acceptable and rejected records and computes summary totals for user approval.
/// </summary>
public record BulkProductPreviewDto(
    int TotalRecords,
    int ValidRecordsCount,
    int InvalidRecordsCount,
    IReadOnlyList<BulkProductRowDto> ValidRecords,
    IReadOnlyList<BulkProductRowDto> InvalidRecords,
    IReadOnlyList<BulkProductRowDto> AllRecords
);
