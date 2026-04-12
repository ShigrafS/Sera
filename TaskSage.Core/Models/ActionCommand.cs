using System.Text.Json.Serialization;

namespace TaskSage.Core.Models;

public class ActionList
{
    [JsonPropertyName("actions")]
    public List<ActionCommand> Actions { get; set; } = new();
}

public class ActionCommand
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("task_ref")]
    public string? TaskRef { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("newDueDate")]
    public string? NewDueDate { get; set; }

    [JsonPropertyName("recurrence")]
    public RecurrenceData? Recurrence { get; set; }

    [JsonPropertyName("question")]
    public string? Question { get; set; }
}

public class RecurrenceData
{
    [JsonPropertyName("frequency")]
    public string Frequency { get; set; } = "daily"; // daily | weekly

    [JsonPropertyName("interval")]
    public int Interval { get; set; } = 1;

    [JsonPropertyName("days")]
    public List<string> DaysOfWeek { get; set; } = new();
}
