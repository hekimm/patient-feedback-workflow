using HastaGeriBildirim.Models.ViewModels;
using HastaGeriBildirim.Repositories;

namespace HastaGeriBildirim.Services;

public class DashboardService
{
    private readonly DashboardRepository _dashboardRepository;
    private readonly KpiRepository _kpiRepository;

    public DashboardService(DashboardRepository dashboardRepository, KpiRepository kpiRepository)
    {
        _dashboardRepository = dashboardRepository;
        _kpiRepository = kpiRepository;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(
        DateTime? startDate,
        DateTime? endDate,
        int? branchId,
        int? departmentId,
        int? doctorId,
        string trendPeriod = "DAY",
        int? userId = null,
        string? roleCode = null)
    {
        var dashboard = await _dashboardRepository.GetDashboardDataAsync(
            startDate, endDate, branchId, departmentId, doctorId, userId, roleCode);

        dashboard.DepartmentSummaries = await _dashboardRepository.GetDepartmentSummariesAsync(
            startDate, endDate, userId, roleCode);

        dashboard.DoctorSummaries = await _dashboardRepository.GetDoctorSummariesAsync(
            startDate, endDate, userId, roleCode);

        dashboard.TrendPeriod = trendPeriod;
        dashboard.TrendData = await _dashboardRepository.GetTrendAsync(startDate, endDate, trendPeriod, userId, roleCode);

        dashboard.AverageCes = await _dashboardRepository.GetCesAverageAsync(startDate, endDate, userId, roleCode);

        var responseRate = await _dashboardRepository.GetResponseRateAsync(startDate, endDate, userId, roleCode);
        dashboard.InvitationsSent = responseRate.SentCount;
        dashboard.ResponseRate = responseRate.SentCount > 0
            ? Math.Round((decimal)responseRate.CompletedCount / responseRate.SentCount * 100, 2)
            : 0;

        dashboard.SentimentDistribution = await _dashboardRepository.GetSentimentDistributionAsync(startDate, endDate, userId, roleCode);

        dashboard.KpiComparisons = await BuildKpiComparisonsAsync(dashboard);

        return dashboard;
    }

    private async Task<List<KpiComparison>> BuildKpiComparisonsAsync(DashboardViewModel dashboard)
    {
        var targets = await _kpiRepository.GetActiveGlobalTargetsAsync();

        return targets.Select(t => new KpiComparison
        {
            KpiCode = t.KpiCode,
            TargetValue = t.TargetValue,
            ActualValue = t.KpiCode switch
            {
                "NPS" => dashboard.NpsScore,
                "CSAT" => dashboard.AverageCsat,
                "CES" => dashboard.AverageCes,
                "NEGATIVE_RATE" => dashboard.NegativePercentage,
                "RESPONSE_RATE" => dashboard.ResponseRate,
                _ => null
            }
        }).ToList();
    }
}
