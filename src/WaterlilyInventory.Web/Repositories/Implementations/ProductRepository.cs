using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WaterlilyInventory.Web.Common.Exceptions;
using WaterlilyInventory.Web.Data;
using WaterlilyInventory.Web.Domain.Entities;
using WaterlilyInventory.Web.DTOs.Reports;
using WaterlilyInventory.Web.Repositories.Interfaces;

namespace WaterlilyInventory.Web.Repositories.Implementations;

/// <summary>
/// Product-specific repository implementing <see cref="IProductRepository"/>.
/// Inherits generic CRUD persistence from <see cref="GenericRepository{Product}"/> and adds
/// execution of the five SQL Server stored-procedure reports via <see cref="RelationalDatabaseFacadeExtensions.SqlQuery{T}"/>.
/// </summary>
public class ProductRepository : GenericRepository<Product>, IProductRepository
{
    private new readonly ILogger<ProductRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ProductRepository"/>.
    /// </summary>
    /// <param name="context">The scoped EF Core database context.</param>
    /// <param name="logger">Logger for product repository and stored-procedure diagnostics.</param>
    public ProductRepository(ApplicationDbContext context, ILogger<ProductRepository> logger)
        : base(context, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CategoryAveragePriceDto>> GetAveragePriceByCategoryAsync(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteReportProcedureAsync<CategoryAveragePriceDto>(
            "dbo.usp_Product_AveragePriceByCategory",
            $"EXEC dbo.usp_Product_AveragePriceByCategory",
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<HighestStockValueCategoryDto>> GetHighestStockValueCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteReportProcedureAsync<HighestStockValueCategoryDto>(
            "dbo.usp_Product_HighestStockValueCategory",
            $"EXEC dbo.usp_Product_HighestStockValueCategory",
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CategoryInventoryValuationDto>> GetInventoryValuationByCategoryAsync(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteReportProcedureAsync<CategoryInventoryValuationDto>(
            "dbo.usp_Product_InventoryValuationByCategory",
            $"EXEC dbo.usp_Product_InventoryValuationByCategory",
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LowStockProductDto>> GetLowStockProductsAsync(
        int threshold = 10,
        CancellationToken cancellationToken = default)
    {
        // Parameterized call ensures safe integer passing without string concatenation
        return await ExecuteReportProcedureAsync<LowStockProductDto>(
            "dbo.usp_Product_LowStock",
            $"EXEC dbo.usp_Product_LowStock @Threshold = {threshold}",
            cancellationToken,
            new { Threshold = threshold });
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TopExpensiveProductDto>> GetTopFiveMostExpensiveProductsAsync(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteReportProcedureAsync<TopExpensiveProductDto>(
            "dbo.usp_Product_TopFiveMostExpensive",
            $"EXEC dbo.usp_Product_TopFiveMostExpensive",
            cancellationToken);
    }

    /// <summary>
    /// Executes a stored procedure using EF Core SqlQuery with unmapped typed DTOs,
    /// measures elapsed execution time, and records structured log entries.
    /// </summary>
    private async Task<IReadOnlyList<TDto>> ExecuteReportProcedureAsync<TDto>(
        string procedureName,
        FormattableString sqlCommand,
        CancellationToken cancellationToken,
        object? safeParameters = null)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Execute stored procedure directly into typed DTOs without intermediate entity mapping
            var results = await _context.Database
                .SqlQuery<TDto>(sqlCommand)
                .ToListAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Executed stored procedure {ProcedureName} with parameters {@Parameters} in {ElapsedMs}ms returning {RowCount} rows. Outcome: Success.",
                procedureName,
                safeParameters ?? "None",
                stopwatch.ElapsedMilliseconds,
                results.Count);

            return results.AsReadOnly();
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "Execution of stored procedure {ProcedureName} was cancelled after {ElapsedMs}ms.",
                procedureName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Stored procedure {ProcedureName} failed after {ElapsedMs}ms.",
                procedureName,
                stopwatch.ElapsedMilliseconds);

            throw new PersistenceException(
                $"Database execution of stored procedure {procedureName} failed.",
                ex);
        }
    }
}
