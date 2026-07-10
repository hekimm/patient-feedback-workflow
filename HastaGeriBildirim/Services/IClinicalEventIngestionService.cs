using HastaGeriBildirim.Models.Api;
using HastaGeriBildirim.Models.Entities;

namespace HastaGeriBildirim.Services;

public interface IClinicalEventIngestionService
{
    Task<int> IngestAsync(ClinicalEventIngestRequest request, CancellationToken cancellationToken = default);
}

public class ClinicalEventIngestionService : IClinicalEventIngestionService
{
    private readonly Repositories.ClinicalEventRepository _clinicalEventRepository;
    private readonly AuditService _auditService;

    public ClinicalEventIngestionService(
        Repositories.ClinicalEventRepository clinicalEventRepository,
        AuditService auditService)
    {
        _clinicalEventRepository = clinicalEventRepository;
        _auditService = auditService;
    }

    public async Task<int> IngestAsync(
        ClinicalEventIngestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.EventType))
            throw new ArgumentException("eventType zorunludur");

        var patientId = request.PatientId;
        if (!patientId.HasValue && request.Patient != null)
        {
            patientId = await _clinicalEventRepository.UpsertPatientAsync(
                new Repositories.ClinicalEventRepository.PatientUpsert
                {
                    PatientId = request.Patient.PatientId,
                    ExternalPatientRef = request.Patient.ExternalPatientRef,
                    FullName = request.Patient.FullName,
                    Phone = request.Patient.Phone,
                    Email = request.Patient.Email,
                    PreferredLanguage = request.Patient.PreferredLanguage ?? "tr",
                    AllowContact = request.Patient.AllowContact ?? true
                });
        }

        var sourceSystem = request.SourceSystem ?? "PROBEL_HBYS";
        var existingEventId = await _clinicalEventRepository.GetEventIdByExternalRefAsync(
            request.ExternalEventRef,
            sourceSystem);

        if (existingEventId.HasValue)
            return existingEventId.Value;

        var clinicalEvent = new ClinicalEvent
        {
            ExternalEventId = request.ExternalEventRef,
            EventType = request.EventType.Trim().ToUpperInvariant(),
            PatientId = patientId,
            HospitalId = request.HospitalId,
            BranchId = request.BranchId,
            DepartmentId = request.DepartmentId,
            DoctorId = request.DoctorId,
            ServiceId = request.ServiceId,
            EventDate = request.EventTime ?? DateTime.Now,
            IsSensitiveCase = request.IsSensitive
        };

        var eventId = await _clinicalEventRepository.CreateEventAsync(clinicalEvent, sourceSystem);

        await _auditService.AddLogAsync(
            "CLINICAL_EVENT",
            eventId,
            "INGESTED",
            null,
            patientId,
            $"Klinik olay alındı: {clinicalEvent.EventType}",
            null);

        return eventId;
    }
}
