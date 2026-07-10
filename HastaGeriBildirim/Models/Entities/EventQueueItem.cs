namespace HastaGeriBildirim.Models.Entities;

public class EventQueueItem
{
    public int QueueId { get; set; }
    public int ClinicalEventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public int RetryCount { get; set; }
    public DateTime ScheduledAt { get; set; }
}
