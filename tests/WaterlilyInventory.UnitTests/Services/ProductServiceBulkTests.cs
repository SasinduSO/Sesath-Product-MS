using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WaterlilyInventory.Web.Caching;
using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.Domain.Entities;
using WaterlilyInventory.Web.DTOs.Products;
using WaterlilyInventory.Web.Repositories.Interfaces;
using WaterlilyInventory.Web.Services.Implementations;
using WaterlilyInventory.Web.Services.Parsing;

namespace WaterlilyInventory.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="ProductService.PreviewBulkProductsAsync"/> and <see cref="ProductService.ImportBulkProductsAsync"/>.
/// Tests validation against the 20 sample reference entries and verifies bulk persistence.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[TestCategory("BulkProductOperations")]
public sealed class ProductServiceBulkTests
{
    private readonly Mock<IProductRepository> _repositoryMock = new(MockBehavior.Strict);
    private readonly Mock<IAppCache> _cacheMock = new(MockBehavior.Strict);
    private readonly ProductSpreadsheetParser _parser = new(NullLogger<ProductSpreadsheetParser>.Instance);

    private ProductService CreateService()
    {
        return new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance,
            _parser);
    }

    [TestMethod]
    public async Task PreviewBulkProducts_ReferenceDataset_Returns15ValidAnd5RejectedWithExactErrors()
    {
        // Arrange: 20 sample items exactly matching the user's reference specification
        const string csvData =
            "Id,Name,Category,Price,Stock\r\n" +
            "1001,Laptop Sleeve 15 Inch,Accessories,$14.99,12\r\n" +
            "1002,Wireless Mouse,Accessories,$19.95,20\r\n" +
            "1003,USB-C Cable,Accessories,$7.50,25\r\n" +
            "1004,Promotional Lanyard,Accessories,$0.00,15\r\n" +
            "1005,LED Monitor 24 Inch,Electronics,$179.99,10\r\n" +
            "1006,Mechanical Keyboard,Electronics,$69.95,8\r\n" +
            "1007,Bluetooth Speaker,Electronics,$39.99,18\r\n" +
            "1008,HD Webcam,Electronics,$49.99,5\r\n" +
            "1009,Ergonomic Office Chair,Office Furniture,$149.99,14\r\n" +
            "1010,Height-Adjustable Desk,Office Furniture,$249.99,6\r\n" +
            "1011,Filing Cabinet,Office Furniture,$119.99,0\r\n" +
            "1012,A4 Notebook,Stationary,$3.99,22\r\n" +
            "1013,Blue Ballpoint Pen,Stationary,$0.75,25\r\n" +
            "1014,Sticky Notes Pack,Stationary,$2.49,9\r\n" +
            "1015,Document Folder,Stationary,$1.25,16\r\n" +
            "1016,Wireless Mouse@Pro,Accessories,$24.99,20\r\n" +
            "1017,Desk Organizer,Kitchen,$9.99,12\r\n" +
            "1018,Portable SSD,Electronics,,10\r\n" +
            "1019,Desk Mat,Accessories,$14.999,15\r\n" +
            "1020,Whiteboard Marker,Stationary,$1.50,-5\r\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvData));
        var service = CreateService();

        // Act
        var result = await service.PreviewBulkProductsAsync(stream, "sample.csv");

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        var preview = result.Data;
        Assert.IsNotNull(preview);

        Assert.AreEqual(20, preview.TotalRecords, "Total records must be 20.");
        Assert.AreEqual(15, preview.ValidRecordsCount, "Valid records must be 15.");
        Assert.AreEqual(5, preview.InvalidRecordsCount, "Invalid records must be 5.");

        // Check the 5 invalid items specifically:
        // 1. Row 1016 (Wireless Mouse@Pro)
        var row16 = preview.InvalidRecords.FirstOrDefault(r => r.RawId == "1016");
        Assert.IsNotNull(row16, "Row 1016 must be rejected.");
        Assert.IsTrue(row16.FieldErrors.ContainsKey(nameof(Product.Name)));
        Assert.IsTrue(row16.Errors.Any(e => e.Contains("special characters", StringComparison.OrdinalIgnoreCase)));

        // 2. Row 1017 (Kitchen)
        var row17 = preview.InvalidRecords.FirstOrDefault(r => r.RawId == "1017");
        Assert.IsNotNull(row17, "Row 1017 must be rejected.");
        Assert.IsTrue(row17.FieldErrors.ContainsKey(nameof(Product.Category)));
        Assert.IsTrue(row17.Errors.Any(e => e.Contains("allowed categories", StringComparison.OrdinalIgnoreCase)));

        // 3. Row 1018 (Missing Price)
        var row18 = preview.InvalidRecords.FirstOrDefault(r => r.RawId == "1018");
        Assert.IsNotNull(row18, "Row 1018 must be rejected.");
        Assert.IsTrue(row18.FieldErrors.ContainsKey(nameof(Product.Price)));
        Assert.IsTrue(row18.Errors.Any(e => e.Contains("Price is required", StringComparison.OrdinalIgnoreCase)));

        // 4. Row 1019 (14.999 - 3 decimal places)
        var row19 = preview.InvalidRecords.FirstOrDefault(r => r.RawId == "1019");
        Assert.IsNotNull(row19, "Row 1019 must be rejected.");
        Assert.IsTrue(row19.FieldErrors.ContainsKey(nameof(Product.Price)));
        Assert.IsTrue(row19.Errors.Any(e => e.Contains("2 decimal places", StringComparison.OrdinalIgnoreCase)));

        // 5. Row 1020 (-5 Stock)
        var row20 = preview.InvalidRecords.FirstOrDefault(r => r.RawId == "1020");
        Assert.IsNotNull(row20, "Row 1020 must be rejected.");
        Assert.IsTrue(row20.FieldErrors.ContainsKey(nameof(Product.Stock)));
        Assert.IsTrue(row20.Errors.Any(e => e.Contains("zero or positive", StringComparison.OrdinalIgnoreCase)));

        // Verify valid items normalize category (e.g. Stationary -> Stationery)
        var notebookRow = preview.ValidRecords.FirstOrDefault(r => r.RawId == "1012");
        Assert.IsNotNull(notebookRow);
        Assert.AreEqual("Stationery", notebookRow.Category);
    }

    [TestMethod]
    public async Task PreviewBulkProducts_EmptyFile_ReturnsValidationFailure()
    {
        // Arrange
        using var stream = new MemoryStream();
        var service = CreateService();

        // Act
        var result = await service.PreviewBulkProductsAsync(stream, "empty.csv");

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
    }

    [TestMethod]
    public async Task ImportBulkProducts_ValidProducts_PersistsRangeAndInvalidatesCache()
    {
        // Arrange
        var products = new List<CreateProductDto>
        {
            new() { Name = "Keyboard", Category = "Electronics", Price = 49.99m, Stock = 10 },
            new() { Name = "Mousepad", Category = "Accessories", Price = 12.50m, Stock = 25 }
        };

        var cancellationToken = CancellationToken.None;

        var persistedEntities = new List<Product>
        {
            new() { Id = 1, Name = "Keyboard", Category = "Electronics", Price = 49.99m, Stock = 10 },
            new() { Id = 2, Name = "Mousepad", Category = "Accessories", Price = 12.50m, Stock = 25 }
        };

        _repositoryMock
            .Setup(r => r.CreateRangeAsync(
                It.Is<IEnumerable<Product>>(list => list.Count() == 2),
                cancellationToken))
            .ReturnsAsync(RepositoryWriteResult<IReadOnlyList<Product>>.Created(persistedEntities));

        _cacheMock
            .Setup(c => c.InvalidateProductAndReports(null));

        var service = CreateService();

        // Act
        var result = await service.ImportBulkProductsAsync(products, cancellationToken);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Data);
        Assert.AreEqual(2, result.Data.ImportedCount);
        Assert.AreEqual(2, result.Data.CreatedIds.Count);

        _repositoryMock.Verify(r => r.CreateRangeAsync(It.IsAny<IEnumerable<Product>>(), cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(null), Times.Once);
    }

    [TestMethod]
    public async Task ImportBulkProducts_EmptyList_ReturnsValidationFailure()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.ImportBulkProductsAsync(new List<CreateProductDto>());

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
    }

    [TestMethod]
    public async Task ImportBulkProducts_InvalidProductInBatch_FailsWithoutCallingRepository()
    {
        // Arrange
        var products = new List<CreateProductDto>
        {
            new() { Name = "Valid Product", Category = "Electronics", Price = 10.00m, Stock = 5 },
            new() { Name = "Invalid@Name", Category = "Electronics", Price = 10.00m, Stock = 5 }
        };

        var service = CreateService();

        // Act
        var result = await service.ImportBulkProductsAsync(products);

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsNotNull(result.Message);
        Assert.IsTrue(result.Message.Contains("failed validation", StringComparison.OrdinalIgnoreCase));

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }
}
