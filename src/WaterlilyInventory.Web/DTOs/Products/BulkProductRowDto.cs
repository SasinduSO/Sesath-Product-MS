namespace WaterlilyInventory.Web.DTOs.Products;

/// <summary>
/// Data transfer object representing a parsed and validated row from a bulk product spreadsheet.
/// Contains original raw values, parsed strongly-typed values, overall validity, and field-level errors.
/// </summary>
public record BulkProductRowDto(
    int RowNumber,
    string? RawId,
    string Name,
    string Category,
    decimal? Price,
    string? RawPrice,
    int? Stock,
    string? RawStock,
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<string, string> FieldErrors
);
