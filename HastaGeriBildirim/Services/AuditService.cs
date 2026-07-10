using HastaGeriBildirim.Models.Entities;
using HastaGeriBildirim.Repositories;

namespace HastaGeriBildirim.Services;

public class AuditService
{
    private readonly AuditLogRepository _auditLogRepository;
    private readonly IPiiCryptoService _piiCryptoService;

    public AuditService(
        AuditLogRepository auditLogRepository,
        IPiiCryptoService piiCryptoService)
    {
        _auditLogRepository = auditLogRepository;
        _piiCryptoService = piiCryptoService;
    }

    public async Task AddLogAsync(
        string entityName, 
        int? entityId, 
        string actionCode, 
        int? performedBy, 
        int? patientId, 
        string? changeDescription, 
        string? ipAddress)
    {
        var log = new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            ActionCode = actionCode,
            PerformedBy = performedBy,
            PatientId = patientId,
            ChangeDescription = changeDescription,
            IpAddress = _piiCryptoService.HashForLookup(ipAddress),
            CreatedAt = DateTime.Now
        };

        await _auditLogRepository.AddLogAsync(log);
    }
}
