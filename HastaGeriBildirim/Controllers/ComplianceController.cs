using System.Text;
using HastaGeriBildirim.Helpers;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Services;
using Microsoft.AspNetCore.Mvc;

namespace HastaGeriBildirim.Controllers;

[RoleAuthorize("SYS_ADMIN")]
public class ComplianceController : BaseController
{
    private readonly ComplianceRepository _complianceRepository;
    private readonly ClinicalEventRepository _clinicalEventRepository;
    private readonly AuditService _auditService;

    public ComplianceController(
        ComplianceRepository complianceRepository,
        ClinicalEventRepository clinicalEventRepository,
        AuditService auditService)
    {
        _complianceRepository = complianceRepository;
        _clinicalEventRepository = clinicalEventRepository;
        _auditService = auditService;
    }

    public async Task<IActionResult> ConsentRecords(DateTime? startDate, DateTime? endDate)
    {
        startDate ??= DateTime.Now.AddDays(-30);
        endDate ??= DateTime.Now;

        var records = await _complianceRepository.GetConsentRecordsAsync(startDate, endDate);

        await _auditService.AddLogAsync(
            "CONSENT_RECORD",
            null,
            "VIEWED",
            HttpContext.GetUserId(),
            null,
            $"Consent records viewed. Start={startDate:yyyy-MM-dd}; End={endDate:yyyy-MM-dd}",
            GetRemoteIp());

        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");

        return View(records);
    }

    public async Task<IActionResult> DataSubjectRequests()
    {
        ViewBag.Patients = await _clinicalEventRepository.GetPatientLookupAsync();
        var requests = await _complianceRepository.GetDataSubjectRequestsAsync();
        return View(requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDataSubjectRequest(int patientId, string requestType, string? note)
    {
        await _complianceRepository.CreateRequestAsync(patientId, requestType, note);
        await _auditService.AddLogAsync(
            "DATA_SUBJECT_REQUEST",
            null,
            "CREATED",
            HttpContext.GetUserId(),
            patientId,
            $"DSR created. Type={requestType}; Note={note}",
            GetRemoteIp());

        return RedirectToAction("DataSubjectRequests");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteDataSubjectRequest(int id, string resolutionNote)
    {
        var request = await _complianceRepository.GetRequestAsync(id);
        if (request == null)
            return NotFound();

        if (request.PatientId.HasValue &&
            request.RequestType is "DELETE" or "ANONYMIZE" or "FORGET")
        {
            await _complianceRepository.AnonymizeResponsesForPatientAsync(request.PatientId.Value);
            await _complianceRepository.ScrubPatientAsync(request.PatientId.Value);
        }

        await _complianceRepository.CompleteRequestAsync(
            id,
            HttpContext.GetUserId()!.Value,
            "COMPLETED",
            resolutionNote);

        await _auditService.AddLogAsync(
            "DATA_SUBJECT_REQUEST",
            id,
            "COMPLETED",
            HttpContext.GetUserId(),
            request.PatientId,
            resolutionNote,
            GetRemoteIp());

        return RedirectToAction("DataSubjectRequests");
    }

    public async Task<IActionResult> ExportPatientData(int id)
    {
        var request = await _complianceRepository.GetRequestAsync(id);
        if (request?.PatientId == null)
            return NotFound();

        var rows = await _complianceRepository.GetPatientExportAsync(request.PatientId.Value);
        var csv = new StringBuilder();
        csv.AppendLine("ResponseId;SubmittedAt;Department;OverallScore;Question;NumericValue;TextValue");
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(';', new[]
            {
                row.ResponseId.ToString(),
                row.SubmittedAt?.ToString("s") ?? "",
                Escape(row.DepartmentName),
                row.OverallScore?.ToString("F2") ?? "",
                Escape(row.QuestionText),
                row.NumericValue?.ToString("F2") ?? "",
                Escape(row.TextValue)
            }));
        }

        await _auditService.AddLogAsync(
            "DATA_SUBJECT_REQUEST",
            id,
            "PATIENT_DATA_EXPORTED",
            HttpContext.GetUserId(),
            request.PatientId,
            "Patient data exported as CSV for DSR fulfillment.",
            GetRemoteIp());

        return File(
            Encoding.UTF8.GetBytes(csv.ToString()),
            "text/csv; charset=utf-8",
            $"hasta-veri-export-{request.PatientId}-{DateTime.Now:yyyyMMddHHmm}.csv");
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private string? GetRemoteIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
