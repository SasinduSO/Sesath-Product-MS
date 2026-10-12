using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WaterlilyInventory.UnitTests.Common;
using WaterlilyInventory.Web.Caching;
using WaterlilyInventory.Web.Domain.Entities;
using WaterlilyInventory.Web.Repositories.Interfaces;
using WaterlilyInventory.Web.Services.Implementations;

namespace WaterlilyInventory.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="ProductService.CreateProductAsync"/>.
/// Exercises the real <see cref="ProductService"/> implementation using strict Moq mocks
/// for repository and cache boundaries, with NullLogger.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[TestCategory("ServiceCrud")]
public sealed class ProductServiceCreateTests
{
    private readonly Mock<IProductRepository> _repositoryMock = new(MockBehavior.Strict);
    private readonly Mock<IAppCache> _cacheMock = new(MockBehavior.Strict);

    /// <summary>
    /// Verifies that valid creation input results in a successful Repository write with mapped entity,
    /// returns an <see cref="WaterlilyInventory.Web.Common.Results.OperationResult{ProductDto}"/> with the generated identity ID,
    /// and triggers cache invalidation for the shared product and report scope.
    /// </summary>
    [TestMethod]
    public async Task CreateProduct_ValidInput_ReturnsSuccessAndInvalidatesCache()
    {
        // Arrange
        const int generatedId = 101;
        var dto = ProductTestBuilder.CreateValidDto();
        var cancellationToken = new CancellationTokenSource().Token;

        _repositoryMock
            .Setup(r => r.CreateAsync(
                It.Is<Product>(p =>
                    p.Name == dto.Name &&
                    p.Category == dto.Category &&
                    p.Price == dto.Price!.Value &&
                    p.Stock == dto.Stock!.Value),
                cancellationToken))
            .ReturnsAsync(ProductTestBuilder.CreateSuccessResult(
                id: generatedId,
                name: dto.Name,
                category: dto.Category,
                price: dto.Price!.Value,
                stock: dto.Stock!.Value));

        _cacheMock
            .Setup(c => c.InvalidateProductAndReports(generatedId));

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.CreateProductAsync(dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result, "Service must return a non-null OperationResult.");
        Assert.IsTrue(result.IsSuccess, "Creation with valid data must return IsSuccess = true.");
        Assert.IsNull(result.ErrorCode, "Successful operation must have null ErrorCode.");
        Assert.IsNull(result.ValidationErrors, "Successful operation must have null ValidationErrors.");
        Assert.IsNotNull(result.Data, "Successful operation must contain the created ProductDto.");

        Assert.AreEqual(generatedId, result.Data.Id, "Returned DTO must contain the generated identity key.");
        Assert.AreEqual(dto.Name, result.Data.Name, "Returned DTO must preserve the product name.");
        Assert.AreEqual(dto.Category, result.Data.Category, "Returned DTO must preserve the normalized category.");
        Assert.AreEqual(dto.Price!.Value, result.Data.Price, "Returned DTO must preserve the price value.");
        Assert.AreEqual(dto.Stock!.Value, result.Data.Stock, "Returned DTO must preserve the stock quantity.");

        // Verify that the repository write occurred exactly once with the intended parameters
        _repositoryMock.Verify(
            r => r.CreateAsync(
                It.Is<Product>(p =>
                    p.Name == dto.Name &&
                    p.Category == dto.Category &&
                    p.Price == dto.Price!.Value &&
                    p.Stock == dto.Stock!.Value),
                cancellationToken),
            Times.Once);

        // Verify that cache invalidation occurred exactly once for the persisted ID
        _cacheMock.Verify(c => c.InvalidateProductAndReports(generatedId), Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }
}
