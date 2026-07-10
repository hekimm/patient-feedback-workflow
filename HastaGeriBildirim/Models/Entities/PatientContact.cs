namespace HastaGeriBildirim.Models.Entities;

public class PatientContact
{
    public int PatientId { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string PreferredLanguage { get; set; } = "tr";
    public bool AllowContact { get; set; }
}
