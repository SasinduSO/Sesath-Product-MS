using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WaterlilyInventory.UnitTests.Common;
using WaterlilyInventory.Web.Caching;
using WaterlilyInventory.Web.Common.Exceptions;
using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.Domain.Entities;
using WaterlilyInventory.Web.DTOs.Products;
using WaterlilyInventory.Web.Delegates;
using WaterlilyInventory.Web.Repositories.Interfaces;
using WaterlilyInventory.Web.Services.Implementations;

namespace WaterlilyInventory.UnitTests.Services;

/// <summary>
/// Unit tests for cancellation token propagation and unexpected exception behavior across ProductService.
/// Exercises the real <see cref="ProductService"/> implementation using strict Moq mocks.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[TestCategory("ServiceCrud")]
public sealed class ProductServiceCancellationTests
{
    private readonly Mock<IProductRepository> _repositoryMock = new(MockBehavior.Strict);
    private readonly Mock<IAppCache> _cacheMock = new(MockBehavior.Strict);

    // =========================================================================
    // 1. CANCELLATION TOKEN PROPAGATION ACROSS CRUD
    // =========================================================================

    [TestMethod]
    public async Task GetAllProducts_CancellationPropagates_DoesNotDisguiseAsPersistenceFailure()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _cacheMock
            .Setup(c => c.CachedAsync(
                CacheKeys.ProductList,
                It.IsAny<AsyncDataFactory<IReadOnlyList<ProductDto>>>(),
                token))
            .ThrowsAsync(new OperationCanceledException(token));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetAllProductsAsync(token));

        _cacheMock.Verify(
            c => c.CachedAsync(
                CacheKeys.ProductList,
                It.IsAny<AsyncDataFactory<IReadOnlyList<ProductDto>>>(),
                token),
            Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task GetProductById_CancellationPropagates_DoesNotDisguiseAsPersistenceFailure()
    {
        // Arrange
        const int productId = 101;
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _cacheMock
            .Setup(c => c.CachedLongAsync(
                CacheKeys.ProductById(productId),
                It.IsAny<AsyncDataFactory<ProductDto>>(),
                token))
            .ThrowsAsync(new OperationCanceledException(token));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetProductByIdAsync(productId, token));

        _cacheMock.Verify(
            c => c.CachedLongAsync(
                CacheKeys.ProductById(productId),
                It.IsAny<AsyncDataFactory<ProductDto>>(),
                token),
            Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task CreateProduct_CancellationPropagates_WithoutCacheInvalidation()
    {
        // Arrange
        var dto = ProductTestBuilder.CreateValidDto();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Product>(), token))
            .ThrowsAsync(new OperationCanceledException(token));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.CreateProductAsync(dto, token));

        _repositoryMock.Verify(r => r.CreateAsync(It.IsAny<Product>(), token), Times.Once);

        // Cancelled write must NOT invalidate cache
        _cacheMock.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task UpdateProduct_CancellationPropagates_WithoutCacheInvalidation()
    {
        // Arrange
        const int productId = 101;
        var dto = ProductTestBuilder.CreateValidUpdateDto();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _repositoryMock
            .Setup(r => r.UpdateAsync(productId, It.IsAny<Action<Product>>(), token))
            .ThrowsAsync(new OperationCanceledException(token));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.UpdateProductAsync(productId, dto, token));

        _repositoryMock.Verify(r => r.UpdateAsync(productId, It.IsAny<Action<Product>>(), token), Times.Once);
        _cacheMock.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DeleteProduct_CancellationPropagates_WithoutCacheInvalidation()
    {
        // Arrange
        const int productId = 101;
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _repositoryMock
            .Setup(r => r.DeleteAsync(productId, token))
            .ThrowsAsync(new OperationCanceledException(token));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.DeleteProductAsync(productId, token));

        _repositoryMock.Verify(r => r.DeleteAsync(productId, token), Times.Once);
        _cacheMock.VerifyNoOtherCalls();
    }

    // =========================================================================
    // 2. UNEXPECTED EXCEPTION PROPAGATION (Escapes to Global Exception Middleware)
    // =========================================================================

    [TestMethod]
    public async Task UnexpectedException_PropagatesToMiddleware_DoesNotDisguiseAsSafeFailure()
    {
        // Arrange
        const int productId = 101;
        var token = CancellationToken.None;

        _cacheMock.SetupCachedLongMiss<ProductDto>(CacheKeys.ProductById(productId));

        // Repository throws an unexpected framework/runtime defect (not PersistenceException)
        _repositoryMock
            .Setup(r => r.GetByIdAsync(productId, token))
            .ThrowsAsync(new InvalidOperationException("Unexpected internal EF Core state defect."));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act & Assert
        // Approved policy: Unexpected defects MUST escape to GlobalExceptionHandlingMiddleware
        // rather than being caught and sanitized into safe OperationResult.PersistenceFailure.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetProductByIdAsync(productId, token));

        _repositoryMock.Verify(r => r.GetByIdAsync(productId, token), Times.Once);
    }

    // =========================================================================
    // 3. INNER EXCEPTION DIAGNOSTIC SUPPRESSION ACROSS CRUD
    // =========================================================================

    [TestMethod]
    public async Task PersistenceException_WithSensitiveInnerException_SuppressesDiagnostics()
    {
        // Arrange
        const int productId = 101;
        const string sensitiveMarker = "Server=sql-prod-cluster;User=sa;Password=P@ssw0rd1234!;Database=SecretDb";
        var innerException = new Exception(sensitiveMarker);
        var persistenceException = new PersistenceException("A database error occurred.", innerException);

        _repositoryMock
            .Setup(r => r.DeleteAsync(productId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(persistenceException);

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.DeleteProductAsync(productId);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.PersistenceFailure, result.ErrorCode);

        // Verify that sensitive credentials in the inner exception do not leak into public message
        Assert.IsFalse(
            result.Message?.Contains("P@ssw0rd1234!", StringComparison.OrdinalIgnoreCase) ?? false,
            "Sensitive inner exception details must never leak into public response message.");
        Assert.IsFalse(
            result.Message?.Contains("sql-prod-cluster", StringComparison.OrdinalIgnoreCase) ?? false);

        _repositoryMock.Verify(r => r.DeleteAsync(productId, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.VerifyNoOtherCalls();
    }
}
