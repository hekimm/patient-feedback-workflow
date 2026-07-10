namespace HastaGeriBildirim.Services.Integrations;

public sealed record BiExportPayload(
    int BiExportId,
    int ResponseId,
    decimal? OverallScore,
    decimal? NpsScore,
    decimal? CsatScore,
    bool IsNegative,
    string? SentimentLabel,
    decimal? SentimentScore,
    int? HospitalId,
    int? BranchId,
    int? DepartmentId,
    int? DoctorId,
    int? ServiceId,
    DateTime? SubmittedAt);

public interface IBiExportClient
{
    Task<IntegrationSendResult> ExportFeedbackAsync(
        BiExportPayload payload,
        CancellationToken cancellationToken = default);
}
