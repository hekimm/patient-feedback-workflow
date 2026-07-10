using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Services;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Helpers;
using System.Text.Json;

namespace HastaGeriBildirim.Controllers;

[RoleAuthorize("QUALITY_MANAGER", "UNIT_MANAGER")]
public class DashboardController : BaseController
{
    private readonly DashboardService _dashboardService;
    private readonly ReportExportService _reportExportService;
    private readonly ReportExportRepository _reportExportRepository;
    private readonly AuditService _auditService;

    public DashboardController(
        DashboardService dashboardService,
        ReportExportService reportExportService,
        ReportExportRepository reportExportRepository,
        AuditService auditService)
    {
        _dashboardService = dashboardService;
        _reportExportService = reportExportService;
        _reportExportRepository = reportExportRepository;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(
        DateTime? startDate, 
        DateTime? endDate,
        int? branchId, 
        int? departmentId, 
        int? doctorId,
        string trendPeriod = "DAY")
    {
        if (!startDate.HasValue)
            startDate = DateTime.Now.AddDays(-30);
        
        if (!endDate.HasValue)
            endDate = DateTime.Now;

        var dashboard = await _dashboardService.GetDashboardAsync(
            startDate, endDate, branchId, departmentId, doctorId, trendPeriod,
            HttpContext.GetUserId(), HttpContext.GetRoleCode());

        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
        ViewBag.TrendPeriod = trendPeriod;

        return View(dashboard);
    }

    public async Task<IActionResult> Export(
        string format,
        DateTime? startDate,
        DateTime? endDate,
        int? branchId,
        int? departmentId,
        int? doctorId,
        string trendPeriod = "DAY")
    {
        if (!startDate.HasValue)
            startDate = DateTime.Now.AddDays(-30);

        if (!endDate.HasValue)
            endDate = DateTime.Now;

        var normalizedFormat = (format ?? "xlsx").ToLowerInvariant();
        if (normalizedFormat is not ("xlsx" or "pdf"))
            return BadRequest("format xlsx veya pdf olmalıdır");

        var filterJson = JsonSerializer.Serialize(new
        {
            startDate,
            endDate,
            branchId,
            departmentId,
            doctorId,
            trendPeriod
        });

        var userId = HttpContext.GetUserId()!.Value;
        var exportId = await _reportExportRepository.CreateExportAsync(
            userId, "FEEDBACK_DASHBOARD", normalizedFormat.ToUpperInvariant(), filterJson);

        try
        {
            var dashboard = await _dashboardService.GetDashboardAsync(
                startDate, endDate, branchId, departmentId, doctorId, trendPeriod,
                HttpContext.GetUserId(), HttpContext.GetRoleCode());

            var bytes = normalizedFormat == "pdf"
                ? _reportExportService.BuildPdf(dashboard, startDate.Value, endDate.Value)
                : _reportExportService.BuildExcel(dashboard, startDate.Value, endDate.Value);

            await _reportExportRepository.MarkCompletedAsync(exportId);
            await _auditService.AddLogAsync(
                "REPORT_EXPORT",
                exportId,
                "COMPLETED",
                userId,
                null,
                $"Dashboard raporu {normalizedFormat.ToUpperInvariant()} olarak dışa aktarıldı",
                null);

            var contentType = normalizedFormat == "pdf"
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            var fileName = $"hasta-memnuniyet-raporu-{DateTime.Now:yyyyMMddHHmm}.{normalizedFormat}";
            return File(bytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            await _reportExportRepository.MarkFailedAsync(exportId, ex.Message);
            throw;
        }
    }
}
