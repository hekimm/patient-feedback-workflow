namespace HastaGeriBildirim.Models.Entities;

public class TriggerRule
{
    public int TriggerRuleId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int SurveyTemplateId { get; set; }
    public string? TemplateName { get; set; }
    public int PrimaryChannelId { get; set; }
    public string? PrimaryChannelName { get; set; }
    public int? FallbackChannelId { get; set; }
    public string? FallbackChannelName { get; set; }
    public bool IsEnabled { get; set; }
    public int DelayMinutes { get; set; }
    public decimal LowScoreThreshold { get; set; }
    public int FrequencyCapDays { get; set; }
    public int FrequencyCapCount { get; set; }
    public bool ReminderEnabled { get; set; }
    public int ReminderCount { get; set; }
    public int ReminderIntervalMinutes { get; set; }
    public int ServiceRecoverySlaHours { get; set; }
    public DateTime CreatedAt { get; set; }
}
