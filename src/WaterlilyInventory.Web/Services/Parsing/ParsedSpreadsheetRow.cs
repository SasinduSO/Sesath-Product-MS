namespace WaterlilyInventory.Web.Services.Parsing;

/// <summary>
/// Intermediate model representing a single raw row extracted from a CSV or Excel spreadsheet.
/// </summary>
public class ParsedSpreadsheetRow
{
    /// <summary>
    /// Line or row number within the source file (1-based, excluding header).
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// Optional row identifier or code provided in the file (e.g., 1001, 1002).
    /// </summary>
    public string? RawId { get; set; }

    /// <summary>
    /// Raw product name string.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Raw category string.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Original raw price text from the spreadsheet (preserves currency symbols or invalid text).
    /// </summary>
    public string RawPrice { get; set; } = string.Empty;

    /// <summary>
    /// Numeric price successfully parsed from the raw text, or null if unparseable or omitted.
    /// </summary>
    public decimal? ParsedPrice { get; set; }

    /// <summary>
    /// Original raw stock text from the spreadsheet.
    /// </summary>
    public string RawStock { get; set; } = string.Empty;

    /// <summary>
    /// Whole integer stock successfully parsed from the raw text, or null if unparseable or omitted.
    /// </summary>
    public int? ParsedStock { get; set; }

    /// <summary>
    /// Any low-level parse errors encountered during row extraction (e.g., malformed format).
    /// </summary>
    public List<string> ParseErrors { get; } = new();
}
