using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.DTOs.Reports;

namespace WaterlilyInventory.Web.Services.Interfaces;

/// <summary>
/// Service interface for the reporting dashboard.
/// Retrieves and caches all five stored-procedure report datasets.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Retrieves the full report dashboard with all five report datasets.
    /// Validates the threshold before Repository calls.
    /// </summary>
    /// <param name="lowStockThreshold">Low-stock threshold. Must be non-negative.</param>
    /// <param name="cancellationToken">Cancellation token for database operations.</param>
    /// <returns>An OperationResult containing the aggregated report dashboard.</returns>
    Task<OperationResult<ReportDashboardDto>> GetDashboardAsync(int lowStockThreshold = 10, CancellationToken cancellationToken = default);
}
