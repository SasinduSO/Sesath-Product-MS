using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace WaterlilyInventory.Web.Services.Parsing;

/// <summary>
/// Robust spreadsheet parser supporting both RFC 4180 CSV files and OpenXML Excel (.xlsx) workbooks.
/// Extracts product data without external NuGet dependencies using .NET BCL ZipArchive and XDocument.
/// </summary>
public class ProductSpreadsheetParser : IProductSpreadsheetParser
{
    private readonly ILogger<ProductSpreadsheetParser> _logger;

    public ProductSpreadsheetParser(ILogger<ProductSpreadsheetParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ParsedSpreadsheetRow>> ParseAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();

        if (extension == ".xlsx")
        {
            return await ParseExcelAsync(fileStream, cancellationToken);
        }

        return await ParseCsvAsync(fileStream, cancellationToken);
    }

    /// <summary>
    /// Parses an RFC 4180 CSV stream, supporting multi-line quotes, custom delimiters, and UTF-8 BOM.
    /// </summary>
    private async Task<IReadOnlyList<ParsedSpreadsheetRow>> ParseCsvAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var rawRows = new List<List<string>>();

        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                return Array.Empty<ParsedSpreadsheetRow>();
            }

            // Determine delimiter: comma, semicolon, or tab
            char delimiter = line.Contains(';') && !line.Contains(',') ? ';' : (line.Contains('\t') ? '\t' : ',');

            var currentRecord = new List<string>();
            var currentField = new StringBuilder();
            bool insideQuotes = false;

            while (line != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];

                    if (insideQuotes)
                    {
                        if (c == '"')
                        {
                            if (i + 1 < line.Length && line[i + 1] == '"')
                            {
                                currentField.Append('"');
                                i++; // Skip escaped quote
                            }
                            else
                            {
                                insideQuotes = false;
                            }
                        }
                        else
                        {
                            currentField.Append(c);
                        }
                    }
                    else
                    {
                        if (c == '"')
                        {
                            insideQuotes = true;
                        }
                        else if (c == delimiter)
                        {
                            currentRecord.Add(currentField.ToString());
                            currentField.Clear();
                        }
                        else
                        {
                            currentField.Append(c);
                        }
                    }
                }

                if (!insideQuotes)
                {
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();

                    if (currentRecord.Any(f => !string.IsNullOrWhiteSpace(f)))
                    {
                        rawRows.Add(currentRecord);
                    }
                    currentRecord = new List<string>();
                }
                else
                {
                    currentField.Append('\n');
                }

                line = await reader.ReadLineAsync(cancellationToken);
            }
        }

        return MapRawRowsToParsedRows(rawRows);
    }

    /// <summary>
    /// Parses an OpenXML Excel (.xlsx) stream using ZipArchive and XDocument.
    /// Extracts shared strings and reads worksheet cell values mapped by column reference.
    /// </summary>
    private async Task<IReadOnlyList<ParsedSpreadsheetRow>> ParseExcelAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var rawRows = new List<List<string>>();

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

            // 1. Load shared strings table if present
            var sharedStrings = new List<string>();
            var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml") ?? archive.GetEntry("xl/SharedStrings.xml");
            if (sharedStringsEntry != null)
            {
                using var ssStream = sharedStringsEntry.Open();
                var ssDoc = XDocument.Load(ssStream);
                var ssNs = ssDoc.Root?.GetDefaultNamespace() ?? XNamespace.None;

                foreach (var si in ssDoc.Root?.Elements(ssNs + "si") ?? Enumerable.Empty<XElement>())
                {
                    var text = string.Concat(si.Descendants(ssNs + "t").Select(t => t.Value));
                    sharedStrings.Add(text);
                }
            }

            // 2. Locate first worksheet
            var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
                ?? archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase));

            if (sheetEntry == null)
            {
                _logger.LogWarning("No worksheet found in Excel workbook.");
                return Array.Empty<ParsedSpreadsheetRow>();
            }

            using (var sheetStream = sheetEntry.Open())
            {
                var sheetDoc = XDocument.Load(sheetStream);
                var sheetNs = sheetDoc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                var rowElements = sheetDoc.Root?.Element(sheetNs + "sheetData")?.Elements(sheetNs + "row");

                if (rowElements != null)
                {
                    foreach (var rowEl in rowElements)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var cellElements = rowEl.Elements(sheetNs + "c").ToList();
                        if (cellElements.Count == 0) continue;

                        // Find maximum column index needed
                        int maxColIndex = 0;
                        var cellMap = new Dictionary<int, string>();

                        foreach (var cEl in cellElements)
                        {
                            var rAttr = cEl.Attribute("r")?.Value;
                            int colIndex = rAttr != null ? ColumnLetterToIndex(rAttr) : maxColIndex;
                            if (colIndex > maxColIndex) maxColIndex = colIndex;

                            var tAttr = cEl.Attribute("t")?.Value;
                            var vEl = cEl.Element(sheetNs + "v");
                            string cellVal = string.Empty;

                            if (tAttr == "s" && vEl != null && int.TryParse(vEl.Value, out int sIndex) && sIndex >= 0 && sIndex < sharedStrings.Count)
                            {
                                cellVal = sharedStrings[sIndex];
                            }
                            else if (tAttr == "inlineStr")
                            {
                                var isEl = cEl.Element(sheetNs + "is");
                                cellVal = string.Concat(isEl?.Descendants(sheetNs + "t").Select(t => t.Value) ?? Enumerable.Empty<string>());
                            }
                            else if (vEl != null)
                            {
                                cellVal = vEl.Value;
                            }

                            cellMap[colIndex] = cellVal;
                        }

                        var rowList = new List<string>(maxColIndex + 1);
                        for (int i = 0; i <= maxColIndex; i++)
                        {
                            rowList.Add(cellMap.TryGetValue(i, out var val) ? val : string.Empty);
                        }

                        if (rowList.Any(val => !string.IsNullOrWhiteSpace(val)))
                        {
                            rawRows.Add(rowList);
                        }
                    }
                }
            }

            return MapRawRowsToParsedRows(rawRows);
        }, cancellationToken);
    }

    /// <summary>
    /// Maps generic rows into typed <see cref="ParsedSpreadsheetRow"/> structures,
    /// intelligently mapping column headers and cleaning numeric fields.
    /// </summary>
    private static IReadOnlyList<ParsedSpreadsheetRow> MapRawRowsToParsedRows(List<List<string>> rawRows)
    {
        if (rawRows.Count == 0)
        {
            return Array.Empty<ParsedSpreadsheetRow>();
        }

        int nameIdx = -1;
        int categoryIdx = -1;
        int priceIdx = -1;
        int stockIdx = -1;
        int idIdx = -1;

        int startRow = 0;
        var firstRow = rawRows[0];

        // Check if first row contains column headers
        bool isHeader = false;
        for (int i = 0; i < firstRow.Count; i++)
        {
            var cell = firstRow[i].Trim().ToLowerInvariant();
            if (cell.Contains("name") || cell.Contains("product"))
            {
                nameIdx = i;
                isHeader = true;
            }
            else if (cell.Contains("category") || cell.Contains("cat"))
            {
                categoryIdx = i;
                isHeader = true;
            }
            else if (cell.Contains("price") || cell.Contains("cost"))
            {
                priceIdx = i;
                isHeader = true;
            }
            else if (cell.Contains("stock") || cell.Contains("qty") || cell.Contains("quantity"))
            {
                stockIdx = i;
                isHeader = true;
            }
            else if (cell == "id" || cell == "code" || cell == "#" || cell == "no" || cell.Contains("product id"))
            {
                idIdx = i;
                isHeader = true;
            }
        }

        if (isHeader)
        {
            startRow = 1;
        }
        else
        {
            // Default positional mapping if no header row detected
            if (firstRow.Count >= 5)
            {
                // [Id, Name, Category, Price, Stock]
                idIdx = 0;
                nameIdx = 1;
                categoryIdx = 2;
                priceIdx = 3;
                stockIdx = 4;
            }
            else
            {
                // [Name, Category, Price, Stock]
                nameIdx = 0;
                categoryIdx = 1;
                priceIdx = 2;
                stockIdx = 3;
            }
        }

        // Fallbacks for any unmapped index
        if (nameIdx == -1) nameIdx = idIdx == 0 ? 1 : 0;
        if (categoryIdx == -1) categoryIdx = nameIdx + 1;
        if (priceIdx == -1) priceIdx = categoryIdx + 1;
        if (stockIdx == -1) stockIdx = priceIdx + 1;

        var result = new List<ParsedSpreadsheetRow>();
        int displayRowNumber = 1;

        for (int r = startRow; r < rawRows.Count; r++)
        {
            var row = rawRows[r];
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue; // Skip blank rows
            }

            var parsedRow = new ParsedSpreadsheetRow
            {
                RowNumber = displayRowNumber++
            };

            // Extract RawId if present
            if (idIdx >= 0 && idIdx < row.Count)
            {
                parsedRow.RawId = row[idIdx].Trim();
            }

            // Extract Name
            if (nameIdx >= 0 && nameIdx < row.Count)
            {
                parsedRow.Name = row[nameIdx].Trim();
            }

            // Extract Category
            if (categoryIdx >= 0 && categoryIdx < row.Count)
            {
                parsedRow.Category = row[categoryIdx].Trim();
            }

            // Extract Price
            if (priceIdx >= 0 && priceIdx < row.Count)
            {
                var rawPrice = row[priceIdx].Trim();
                parsedRow.RawPrice = rawPrice;

                // Clean price string: remove currency symbols and thousands separators
                var cleanPrice = Regex.Replace(rawPrice, @"[^\d.-]", "");
                if (!string.IsNullOrWhiteSpace(cleanPrice) &&
                    decimal.TryParse(cleanPrice, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price))
                {
                    parsedRow.ParsedPrice = price;
                }
            }

            // Extract Stock
            if (stockIdx >= 0 && stockIdx < row.Count)
            {
                var rawStock = row[stockIdx].Trim();
                parsedRow.RawStock = rawStock;

                // Strip non-numeric except minus sign
                var cleanStock = rawStock.Replace(",", "").Trim();
                if (!string.IsNullOrWhiteSpace(cleanStock) &&
                    int.TryParse(cleanStock, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stock))
                {
                    parsedRow.ParsedStock = stock;
                }
            }

            result.Add(parsedRow);
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// Converts Excel column letters (e.g., "A", "B", "AA") into 0-indexed column integer numbers.
    /// </summary>
    private static int ColumnLetterToIndex(string cellReference)
    {
        var letters = new string(cellReference.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
        int sum = 0;
        foreach (var c in letters)
        {
            sum = (sum * 26) + (c - 'A' + 1);
        }
        return Math.Max(0, sum - 1);
    }
}
