using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.Services.Interfaces;

namespace WaterlilyInventory.Web.Controllers;

/// <summary>
/// Controller for inventory reports dashboard page navigation and AJAX data endpoints.
/// Thin web boundary: delegates report retrieval and caching to <see cref="IReportService"/>.
/// Never accesses DbContext, repositories, SQL, or cache directly.
/// </summary>
[Route("reports")]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(IReportService reportService, ILogger<ReportsController> logger)
    {
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// GET /reports
    /// Returns the main Reporting Dashboard page shell.
    /// </summary>
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// GET /reports/data?threshold=10
    /// Returns the complete report dashboard as JSON.
    /// </summary>
    /// <param name="threshold">Stock threshold for low-stock product filter. Default is 10.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("data")]
    public async Task<IActionResult> GetData([FromQuery] int threshold = 10, CancellationToken cancellationToken = default)
    {
        var result = await _reportService.GetDashboardAsync(threshold, cancellationToken);
        return result.ToActionResult(this);
    }
}
