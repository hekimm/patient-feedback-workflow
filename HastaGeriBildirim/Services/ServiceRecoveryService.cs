using HastaGeriBildirim.Models.Entities;
using HastaGeriBildirim.Repositories;

namespace HastaGeriBildirim.Services;

public class ServiceRecoveryService
{
    private readonly ServiceRecoveryRepository _recoveryRepository;
    private readonly AuditService _auditService;

    public ServiceRecoveryService(
        ServiceRecoveryRepository recoveryRepository,
        AuditService auditService)
    {
        _recoveryRepository = recoveryRepository;
        _auditService = auditService;
    }

    public async Task AssignCaseAsync(int caseId, int userId, int performedBy)
    {
        await _recoveryRepository.AssignCaseAsync(caseId, userId);
        await RecordActionAsync(caseId, "ASSIGN", $"Case kullanıcıya atandı: {userId}",
            performedBy, "ASSIGNED", $"Case atandı: {userId}");
    }

    public async Task AddActionAsync(int caseId, string actionNote, int performedBy)
    {
        await RecordActionAsync(caseId, "NOTE", actionNote,
            performedBy, "ACTION_ADDED", actionNote);
    }

    public async Task CloseCaseAsync(int caseId, string closureNote, int performedBy)
    {
        await _recoveryRepository.CloseCaseAsync(caseId, closureNote);
        await RecordActionAsync(caseId, "CLOSE", closureNote,
            performedBy, "CLOSED", closureNote);
    }

    public async Task EscalateCaseAsync(int caseId, int performedBy)
    {
        await _recoveryRepository.UpdateCaseStatusAsync(caseId, "ESCALATED");
        await RecordActionAsync(caseId, "ESCALATE", "Case üst kademeye yükseltildi",
            performedBy, "ESCALATED", "Üst kademeye yükseltildi");
    }

    private async Task RecordActionAsync(
        int caseId, string actionType, string actionNote,
        int performedBy, string auditCode, string auditMessage)
    {
        var action = new RecoveryAction
        {
            CaseId = caseId,
            ActionType = actionType,
            PerformedBy = performedBy,
            ActionNote = actionNote,
            CreatedAt = DateTime.Now
        };

        await _recoveryRepository.AddActionAsync(action);
        await _auditService.AddLogAsync(
            "RECOVERY_CASE", caseId, auditCode, performedBy, null, auditMessage, null);
    }
}
