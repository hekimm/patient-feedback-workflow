namespace HastaGeriBildirim.Models.Entities;

public class KpiTarget
{
    public int KpiTargetId { get; set; }
    public string KpiCode { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string TargetPeriod { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public DateTime CreatedAt { get; set; }
}
