using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using WaterlilyInventory.Web.Services.Parsing;

namespace WaterlilyInventory.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="ProductSpreadsheetParser"/>.
/// Tests RFC 4180 CSV parsing, currency cleaning, header mapping, missing cells, and Excel .xlsx extraction.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[TestCategory("SpreadsheetParsing")]
public sealed class ProductSpreadsheetParserTests
{
    private readonly ProductSpreadsheetParser _parser = new(NullLogger<ProductSpreadsheetParser>.Instance);

    [TestMethod]
    public async Task ParseAsync_StandardCsvWithHeaders_MapsFieldsCorrectly()
    {
        // Arrange
        const string csv = "Name,Category,Price,Stock\r\n" +
                           "Laptop Sleeve 15 Inch,Accessories,14.99,12\r\n" +
                           "Wireless Mouse,Accessories,19.95,20\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var rows = await _parser.ParseAsync(stream, "products.csv");

        // Assert
        Assert.IsNotNull(rows);
        Assert.AreEqual(2, rows.Count);

        Assert.AreEqual("Laptop Sleeve 15 Inch", rows[0].Name);
        Assert.AreEqual("Accessories", rows[0].Category);
        Assert.AreEqual(14.99m, rows[0].ParsedPrice);
        Assert.AreEqual(12, rows[0].ParsedStock);

        Assert.AreEqual("Wireless Mouse", rows[1].Name);
        Assert.AreEqual(19.95m, rows[1].ParsedPrice);
        Assert.AreEqual(20, rows[1].ParsedStock);
    }

    [TestMethod]
    public async Task ParseAsync_CsvWithIdColumn_MapsRawIdAndAttributes()
    {
        // Arrange
        const string csv = "Id,Name,Category,Price,Stock\r\n" +
                           "1001,Laptop Sleeve 15 Inch,Accessories,$14.99,12\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var rows = await _parser.ParseAsync(stream, "products.csv");

        // Assert
        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual("1001", rows[0].RawId);
        Assert.AreEqual("Laptop Sleeve 15 Inch", rows[0].Name);
        Assert.AreEqual(14.99m, rows[0].ParsedPrice);
        Assert.AreEqual(12, rows[0].ParsedStock);
    }

    [TestMethod]
    public async Task ParseAsync_CsvWithCurrencyAndCommas_ExtractsNumericPrice()
    {
        // Arrange
        const string csv = "Name,Category,Price,Stock\r\n" +
                           "Ergonomic Chair,Office Furniture,\"$1,250.50\",5\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var rows = await _parser.ParseAsync(stream, "data.csv");

        // Assert
        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual(1250.50m, rows[0].ParsedPrice);
        Assert.AreEqual(5, rows[0].ParsedStock);
    }

    [TestMethod]
    public async Task ParseAsync_EmptyStream_ReturnsEmptyCollection()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var rows = await _parser.ParseAsync(stream, "empty.csv");

        // Assert
        Assert.AreEqual(0, rows.Count);
    }

    [TestMethod]
    public async Task ParseAsync_MinimalExcelFile_ParsesWorksheetRows()
    {
        // Arrange: Generate a minimal in-memory .xlsx ZIP archive
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // xl/sharedStrings.xml
            var ssEntry = archive.CreateEntry("xl/sharedStrings.xml");
            using (var ssStream = ssEntry.Open())
            using (var writer = new StreamWriter(ssStream, Encoding.UTF8))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                             "<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"4\" uniqueCount=\"4\">" +
                             "<si><t>Name</t></si>" +
                             "<si><t>Category</t></si>" +
                             "<si><t>Gaming Mouse</t></si>" +
                             "<si><t>Accessories</t></si>" +
                             "</sst>");
            }

            // xl/worksheets/sheet1.xml
            var sheetEntry = archive.CreateEntry("xl/worksheets/sheet1.xml");
            using (var sheetStream = sheetEntry.Open())
            using (var writer = new StreamWriter(sheetStream, Encoding.UTF8))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                             "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                             "<sheetData>" +
                             "<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\" t=\"s\"><v>1</v></c><c r=\"C1\"><v>Price</v></c><c r=\"D1\"><v>Stock</v></c></row>" +
                             "<row r=\"2\"><c r=\"A2\" t=\"s\"><v>2</v></c><c r=\"B2\" t=\"s\"><v>3</v></c><c r=\"C2\"><v>29.99</v></c><c r=\"D2\"><v>15</v></c></row>" +
                             "</sheetData>" +
                             "</worksheet>");
            }
        }

        memoryStream.Position = 0;

        // Act
        var rows = await _parser.ParseAsync(memoryStream, "inventory.xlsx");

        // Assert
        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual("Gaming Mouse", rows[0].Name);
        Assert.AreEqual("Accessories", rows[0].Category);
        Assert.AreEqual(29.99m, rows[0].ParsedPrice);
        Assert.AreEqual(15, rows[0].ParsedStock);
    }
}
