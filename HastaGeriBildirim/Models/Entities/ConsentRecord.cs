namespace HastaGeriBildirim.Models.Entities;

public class ConsentRecord
{
    public int ConsentId { get; set; }
    public int? PatientId { get; set; }
    public int InvitationId { get; set; }
    public int ConsentTextId { get; set; }
    public bool IsConsentGiven { get; set; }
    public bool IsAnonymous { get; set; }
    public string? IpAddress { get; set; }
    public DateTime ConsentDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
