using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Helpers;
using HastaGeriBildirim.Models.Entities;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Services;

namespace HastaGeriBildirim.Controllers;

[RoleAuthorize("QUALITY_MANAGER")]
public class ClinicalEventsController : BaseController
{
    private readonly ClinicalEventRepository _eventRepository;
    private readonly SurveyDispatchService _dispatchService;
    private readonly AuditService _auditService;

    public ClinicalEventsController(
        ClinicalEventRepository eventRepository,
        SurveyDispatchService dispatchService,
        AuditService auditService)
    {
        _eventRepository = eventRepository;
        _dispatchService = dispatchService;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var events = await _eventRepository.GetRecentEventsAsync(50);

        ViewBag.Patients = await _eventRepository.GetPatientLookupAsync();
        ViewBag.Departments = await _eventRepository.GetDepartmentLookupAsync();
        ViewBag.Doctors = await _eventRepository.GetDoctorLookupAsync();
        ViewBag.Services = await _eventRepository.GetServiceLookupAsync();

        return View(events);
    }

    [HttpPost]
    public async Task<IActionResult> ManualCreate(
        string eventType, int patientId, int departmentId, int? doctorId, int? serviceId, bool isSensitive)
    {
        var departments = await _eventRepository.GetDepartmentLookupAsync();
        var department = departments.FirstOrDefault(d => d.Id == departmentId);
        if (department == null)
        {
            TempData["Message"] = "Geçersiz bölüm seçimi.";
            return RedirectToAction("Index");
        }

        var clinicalEvent = new ClinicalEvent
        {
            ExternalEventId = "MANUAL-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            EventType = eventType,
            PatientId = patientId,
            HospitalId = department.HospitalId,
            BranchId = department.BranchId,
            DepartmentId = departmentId,
            DoctorId = doctorId,
            ServiceId = serviceId,
            EventDate = DateTime.Now,
            IsSensitiveCase = isSensitive
        };

        var eventId = await _eventRepository.CreateEventAsync(clinicalEvent, "PROBEL_HBYS");

        await _auditService.AddLogAsync(
            "CLINICAL_EVENT", eventId, "MANUAL_CREATED",
            HttpContext.GetUserId(), patientId, $"Manuel HBYS klinik olayı oluşturuldu: {eventType}", null);

        TempData["Message"] = $"Klinik olay oluşturuldu (#{eventId}). Tetikleme motoru olayı işleyecek.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> ProcessNow()
    {
        await _dispatchService.RunAllAsync();
        TempData["Message"] = "Tetikleme motoru çalıştırıldı: bekleyen olaylar ve kuyruk işlendi.";
        return RedirectToAction("Index");
    }
}
