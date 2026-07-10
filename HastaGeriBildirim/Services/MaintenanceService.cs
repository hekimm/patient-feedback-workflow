using System.Text.Json;
using HastaGeriBildirim.Models.Entities;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Services.Integrations;

namespace HastaGeriBildirim.Services;

public class MaintenanceService
{
    private readonly MaintenanceRepository _maintenanceRepository;
    private readonly AlertRepository _alertRepository;
    private readonly DispatchRepository _dispatchRepository;
    private readonly WebhookReplayRepository _webhookReplayRepository;
    private readonly AuditService _auditService;
    private readonly IBiExportClient _biExportClient;

    public MaintenanceService(
        MaintenanceRepository maintenanceRepository,
        AlertRepository alertRepository,
        DispatchRepository dispatchRepository,
        WebhookReplayRepository webhookReplayRepository,
        AuditService auditService,
        IBiExportClient biExportClient)
    {
        _maintenanceRepository = maintenanceRepository;
        _alertRepository = alertRepository;
        _dispatchRepository = dispatchRepository;
        _webhookReplayRepository = webhookReplayRepository;
        _auditService = auditService;
        _biExportClient = biExportClient;
    }

    public async Task RunAllAsync()
    {
        await EscalateOverdueCasesAsync();
        await ApplyRetentionPoliciesAsync();
        await _webhookReplayRepository.DeleteExpiredAsync();
        await ProcessBiExportQueueAsync();
    }

    public async Task EscalateOverdueCasesAsync()
    {
        var overdueCases = await _maintenanceRepository.GetOverdueCasesAsync();

        foreach (var overdueCase in overdueCases)
        {
            await _maintenanceRepository.EscalateCaseAsync(overdueCase.CaseId);
            await _maintenanceRepository.InsertEscalationAsync(
                overdueCase.CaseId, "SLA süresi aşıldı; kayıt otomatik olarak üst kademeye yükseltildi");
            await _maintenanceRepository.InsertSystemActionAsync(
                overdueCase.CaseId, "ESCALATE", "SLA aşımı nedeniyle otomatik eskalasyon");

            var alert = new Alert
            {
                AlertType = "SLA_BREACH",
                Severity = "HIGH",
                Status = "OPEN",
                ResponseId = overdueCase.ResponseId,
                Message = $"Hizmet telafisi #{overdueCase.CaseId} için SLA süresi aşıldı"
            };
            await _alertRepository.CreateAlertAsync(alert);

            await _auditService.AddLogAsync(
                "RECOVERY_CASE", overdueCase.CaseId, "AUTO_ESCALATED",
                null, null, "SLA aşımı: otomatik eskalasyon", null);
        }
    }

    public async Task ApplyRetentionPoliciesAsync()
    {
        var responsePolicy = await _maintenanceRepository.GetRetentionPolicyAsync("SURVEY_RESPONSE");
        if (responsePolicy != null && responsePolicy.ActionAfterRetention == "ANONYMIZE")
        {
            var anonymized = await _maintenanceRepository.AnonymizeOldResponsesAsync(responsePolicy.RetentionDays);
            if (anonymized > 0)
            {
                await _auditService.AddLogAsync(
                    "SURVEY_RESPONSE", null, "RETENTION_ANONYMIZED",
                    null, null, $"Saklama süresi dolan {anonymized} yanıt anonimleştirildi", null);
            }
        }

        var auditPolicy = await _maintenanceRepository.GetRetentionPolicyAsync("AUDIT_LOG");
        if (auditPolicy != null && auditPolicy.ActionAfterRetention == "DELETE")
        {
            var deleted = await _maintenanceRepository.DeleteOldAuditLogsAsync(auditPolicy.RetentionDays);
            if (deleted > 0)
            {
                await _auditService.AddLogAsync(
                    "AUDIT_LOG", null, "RETENTION_DELETED",
                    null, null, $"Saklama süresi dolan {deleted} denetim kaydı silindi", null);
            }
        }
    }

    public async Task ProcessBiExportQueueAsync()
    {
        var pendingItems = await _maintenanceRepository.GetWaitingBiExportsAsync();

        foreach (var item in pendingItems)
        {
            var result = await _biExportClient.ExportFeedbackAsync(item.ToPayload());
            var payload = JsonSerializer.Serialize(new { biExportId = item.BiExportId, responseId = item.ResponseId });

            if (result.Success)
                await _maintenanceRepository.MarkBiExportedAsync(item.BiExportId);
            else
                await _maintenanceRepository.MarkBiExportFailedAsync(item.BiExportId, result.ErrorMessage);

            await _dispatchRepository.InsertIntegrationLogAsync(
                "PROBEL_BI",
                "OUTBOUND",
                "BI_FEEDBACK_EXPORT",
                payload,
                result.Success,
                result.ErrorMessage,
                result.ResponsePayload,
                result.HttpStatusCode,
                result.ProviderMessageId);
        }
    }
}
