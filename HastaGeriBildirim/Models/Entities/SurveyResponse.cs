namespace HastaGeriBildirim.Models.Entities;

public class SurveyResponse
{
    public int ResponseId { get; set; }
    public int InvitationId { get; set; }
    public int? PatientId { get; set; }
    public int ClinicalEventId { get; set; }
    public int TemplateVersionId { get; set; }
    public int? HospitalId { get; set; }
    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int? DoctorId { get; set; }
    public int? ServiceId { get; set; }
    public int? ConsentRecordId { get; set; }
    public bool IsAnonymous { get; set; }
    public decimal? OverallScore { get; set; }
    public decimal? NpsScore { get; set; }
    public decimal? CsatScore { get; set; }
    public bool IsNegative { get; set; }
    public string? SentimentLabel { get; set; }
    public decimal? SentimentScore { get; set; }
    public DateTime CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
