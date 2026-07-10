namespace HastaGeriBildirim.Models.Entities;

public class Channel
{
    public int ChannelId { get; set; }
    public string ChannelCode { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
