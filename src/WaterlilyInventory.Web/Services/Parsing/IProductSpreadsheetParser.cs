namespace WaterlilyInventory.Web.Services.Parsing;

/// <summary>
/// Parser interface for extracting structured product rows from uploaded spreadsheet streams (.csv or .xlsx).
/// Operates purely on streams and file metadata without depending on HttpContext or web framework types.
/// </summary>
public interface IProductSpreadsheetParser
{
    /// <summary>
    /// Parses an input stream of a spreadsheet (CSV or Excel) into a read-only list of raw product rows.
    /// </summary>
    /// <param name="fileStream">The readable data stream.</param>
    /// <param name="fileName">The original filename used to detect extension (.csv, .xlsx, .txt).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only collection of parsed spreadsheet rows.</returns>
    Task<IReadOnlyList<ParsedSpreadsheetRow>> ParseAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
