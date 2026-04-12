using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace TaskSage.Data.Entities;

public enum TaskStatus { Pending, Completed, Skipped }
public enum Priority { Low, Medium, High }

public class TaskInstance
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public DateOnly DueDate { get; set; } // pure date for rollover
    public int? TemplateId { get; set; } // null = one-off task
    public Priority Priority { get; set; } = Priority.Medium;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class RecurringTemplate
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    
    // Store as JSON in DB for simplicity
    public string RecurrenceJson { get; set; } = "{}";
    
    [NotMapped]
    public RecurrenceRule Recurrence
    {
        get => JsonSerializer.Deserialize<RecurrenceRule>(RecurrenceJson) ?? new RecurrenceRule();
        set => RecurrenceJson = JsonSerializer.Serialize(value);
    }
    
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}

public class RecurrenceRule
{
    public string Frequency { get; set; } = "daily"; // daily | weekly
    public int Interval { get; set; } = 1; // every N days
    public List<string> DaysOfWeek { get; set; } = new(); // monday, tuesday...
}

public class AppSettings
{
    public int Id { get; set; } = 1; // Fixed id for singleton settings
    public string? NvidiaApiKey { get; set; } 
    public string SelectedModel { get; set; } = "meta/llama-3.1-70b-instruct";
    public string BaseUrl { get; set; } = "https://integrate.api.nvidia.com/v1";
    public DateTimeOffset LastUpdated { get; set; }
}
