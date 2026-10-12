using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WaterlilyInventory.UnitTests.Common;
using WaterlilyInventory.Web.Caching;
using WaterlilyInventory.Web.Common.Exceptions;
using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.Domain.Entities;
using WaterlilyInventory.Web.DTOs.Products;
using WaterlilyInventory.Web.Repositories.Interfaces;
using WaterlilyInventory.Web.Services.Implementations;

namespace WaterlilyInventory.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="ProductService.DeleteProductAsync"/>.
/// Exercises the real <see cref="ProductService"/> implementation using strict Moq mocks
/// for repository and cache boundaries, with NullLogger.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[TestCategory("ServiceCrud")]
public sealed class ProductServiceDeleteTests
{
    private readonly Mock<IProductRepository> _repositoryMock = new(MockBehavior.Strict);
    private readonly Mock<IAppCache> _cacheMock = new(MockBehavior.Strict);

    /// <summary>
    /// Verifies that a successful repository deletion returns an explicit success outcome
    /// with the correct deleted Id in DeleteProductResultDto and invalidates the cache scope.
    /// </summary>
    [TestMethod]
    public async Task DeleteProduct_ExistingId_ReturnsSuccessAndInvalidatesCache()
    {
        // Arrange
        const int productId = 101;
        var cancellationToken = new CancellationTokenSource().Token;
        var deletedEntity = ProductTestBuilder.CreateProduct(productId);

        _repositoryMock
            .Setup(r => r.DeleteAsync(productId, cancellationToken))
            .ReturnsAsync(RepositoryWriteResult<Product>.Deleted(deletedEntity));

        _cacheMock
            .Setup(c => c.InvalidateProductAndReports(productId));

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.DeleteProductAsync(productId, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess, "Deletion of existing product must succeed.");
        Assert.IsNull(result.ErrorCode);
        Assert.IsNotNull(result.Data);
        Assert.AreEqual(productId, result.Data.DeletedId, "Returned DTO must contain the deleted Id.");

        _repositoryMock.Verify(r => r.DeleteAsync(productId, cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(productId), Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that attempting to delete a non-existent product returns NotFound without cache invalidation.
    /// </summary>
    [TestMethod]
    public async Task DeleteProduct_MissingProduct_ReturnsNotFoundWithoutInvalidation()
    {
        // Arrange
        const int nonExistentId = 999;
        var cancellationToken = new CancellationTokenSource().Token;

        _repositoryMock
            .Setup(r => r.DeleteAsync(nonExistentId, cancellationToken))
            .ReturnsAsync(RepositoryWriteResult<Product>.NotFoundResult());

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.DeleteProductAsync(nonExistentId, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.NotFound, result.ErrorCode);
        Assert.IsNull(result.Data);

        _repositoryMock.Verify(r => r.DeleteAsync(nonExistentId, cancellationToken), Times.Once);
        _cacheMock.VerifyNoOtherCalls();
        _repositoryMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that an invalid Id (zero or negative) is rejected at the service boundary
    /// before performing any repository or cache mutations.
    /// </summary>
    [TestMethod]
    [DataRow(0, DisplayName = "Id = 0")]
    [DataRow(-1, DisplayName = "Id = -1")]
    [DataRow(-100, DisplayName = "Id = -100")]
    public async Task DeleteProduct_InvalidId_ZeroOrNegative_ReturnsValidationFailureWithoutDataAccess(int invalidId)
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource().Token;

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.DeleteProductAsync(invalidId, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsNull(result.Data);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that a concurrency conflict during deletion returns Conflict without cache invalidation.
    /// </summary>
    [TestMethod]
    public async Task DeleteProduct_ConcurrencyConflict_ReturnsConflictWithoutInvalidation()
    {
        // Arrange
        const int productId = 101;
        var cancellationToken = new CancellationTokenSource().Token;

        _repositoryMock
            .Setup(r => r.DeleteAsync(productId, cancellationToken))
            .ReturnsAsync(RepositoryWriteResult<Product>.ConflictResult());

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.DeleteProductAsync(productId, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Conflict, result.ErrorCode);
        Assert.IsNull(result.Data);

        _repositoryMock.Verify(r => r.DeleteAsync(productId, cancellationToken), Times.Once);
        _cacheMock.VerifyNoOtherCalls();
        _repositoryMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that an expected persistence exception during deletion returns safe PersistenceFailure
    /// without retrying the mutation, invalidating caches, or leaking internal diagnostics.
    /// </summary>
    [TestMethod]
    public async Task DeleteProduct_PersistenceException_ReturnsSafeFailureWithoutRetryOrInvalidation()
    {
        // Arrange
        const int productId = 101;
        const string diagnosticMarker = "FOREIGN KEY constraint FK_OrderDetails_Product conflicted [Error 547]";
        var cancellationToken = new CancellationTokenSource().Token;

        _repositoryMock
            .Setup(r => r.DeleteAsync(productId, cancellationToken))
            .ThrowsAsync(new PersistenceException(diagnosticMarker));

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.DeleteProductAsync(productId, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.PersistenceFailure, result.ErrorCode);
        Assert.IsNull(result.Data);

        // Diagnostic suppression
        Assert.IsFalse(
            result.Message?.Contains(diagnosticMarker, StringComparison.OrdinalIgnoreCase) ?? false,
            "Internal diagnostic marker must not be leaked in public Message.");

        _repositoryMock.Verify(r => r.DeleteAsync(productId, cancellationToken), Times.Once);
        _cacheMock.VerifyNoOtherCalls();
        _repositoryMock.VerifyNoOtherCalls();
    }

    // =========================================================================
    // COMMIT ORDERING TESTS
    // =========================================================================

    /// <summary>
    /// Commit Ordering: Proves that cache invalidation cannot precede a successful deletion.
    /// While the repository delete is pending in-flight, invalidation must not have occurred.
    /// Once the delete completes successfully, invalidation executes exactly once.
    /// </summary>
    [TestMethod]
    public async Task DeleteProduct_CommitOrdering_InvalidationCannotPrecedeSuccessfulDeletion()
    {
        // Arrange
        const int productId = 101;
        var cancellationToken = new CancellationTokenSource().Token;

        var deleteEnteredSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var deleteCompletionSource = new TaskCompletionSource<RepositoryWriteResult<Product>>(TaskCreationOptions.RunContinuationsAsynchronously);

        _repositoryMock
            .Setup(r => r.DeleteAsync(productId, cancellationToken))
            .Returns(async () =>
            {
                deleteEnteredSignal.TrySetResult(true);
                return await deleteCompletionSource.Task;
            });

        _cacheMock
            .Setup(c => c.InvalidateProductAndReports(productId));

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act - Start deletion
        var serviceTask = service.DeleteProductAsync(productId, cancellationToken);

        // Wait until repository delete is entered
        var entered = await deleteEnteredSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(entered, "Service execution must reach the repository delete.");

        // ASSERT: Invalidation must NOT precede the completed deletion
        _cacheMock.Verify(
            c => c.InvalidateProductAndReports(It.IsAny<int?>()),
            Times.Never,
            "Cache invalidation must never precede a successful persistence outcome.");

        // Complete the repository delete successfully
        var deletedEntity = ProductTestBuilder.CreateProduct(productId);
        deleteCompletionSource.TrySetResult(RepositoryWriteResult<Product>.Deleted(deletedEntity));

        // Await service completion
        var result = await serviceTask.WaitAsync(TimeSpan.FromSeconds(5));

        // ASSERT: Once deletion confirmed, success and invalidation are finalized
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(productId), Times.Once);

        _repositoryMock.Verify(r => r.DeleteAsync(productId, cancellationToken), Times.Once);
        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Commit Ordering Failure Path: Proves that when an in-flight deletion fails or throws,
    /// cache invalidation is never invoked.
    /// </summary>
    [TestMethod]
    public async Task DeleteProduct_CommitOrdering_UnsuccessfulDeletionDoesNotInvalidate()
    {
        // Arrange
        const int productId = 101;
        var cancellationToken = new CancellationTokenSource().Token;

        var deleteEnteredSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var deleteCompletionSource = new TaskCompletionSource<RepositoryWriteResult<Product>>(TaskCreationOptions.RunContinuationsAsynchronously);

        _repositoryMock
            .Setup(r => r.DeleteAsync(productId, cancellationToken))
            .Returns(async () =>
            {
                deleteEnteredSignal.TrySetResult(true);
                return await deleteCompletionSource.Task;
            });

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act - Start deletion
        var serviceTask = service.DeleteProductAsync(productId, cancellationToken);

        var entered = await deleteEnteredSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(entered);

        // Complete delete with an uncommitted failure (PersistenceException)
        deleteCompletionSource.TrySetException(new PersistenceException("Disk I/O error during delete commit"));

        var result = await serviceTask.WaitAsync(TimeSpan.FromSeconds(5));

        // ASSERT: Result is failure, and cache was never invalidated
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.PersistenceFailure, result.ErrorCode);

        _cacheMock.Verify(
            c => c.InvalidateProductAndReports(It.IsAny<int?>()),
            Times.Never,
            "Failed deletion must never trigger cache invalidation.");

        _repositoryMock.Verify(r => r.DeleteAsync(productId, cancellationToken), Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }
}
