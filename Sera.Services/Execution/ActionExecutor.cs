using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sera.Core.Models;
using Sera.Data;
using Sera.Data.Entities;

namespace Sera.Services.Execution;

public interface IActionExecutor
{
    Task<ExecutionResult> ExecuteAsync(IEnumerable<ActionCommand> actions);
}

public class ExecutionResult
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public string? ClarificationQuestion { get; set; }
}

public class ActionExecutor : IActionExecutor
{
    private readonly SeraDbContext _db;

    public ActionExecutor(SeraDbContext db)
    {
        _db = db;
    }

    public async Task<ExecutionResult> ExecuteAsync(IEnumerable<ActionCommand> actions)
    {
        var result = new ExecutionResult();
        var pendingTasks = await _db.Tasks.Where(t => t.Status == Data.Entities.TaskStatus.Pending).ToListAsync();

        foreach (var act in actions)
        {
            switch (act.Action.ToLowerInvariant())
            {
                case "add_task":
                    var newTask = new TaskInstance
                    {
                        Title = act.Title ?? "Untitled Task",
                        DueDate = ParseDate(act.NewDueDate) ?? DateOnly.FromDateTime(DateTime.Now),
                        CreatedAt = DateTimeOffset.Now,
                        Status = Data.Entities.TaskStatus.Pending
                    };
                    _db.Tasks.Add(newTask);
                    break;

                case "complete_task":
                case "delete_task":
                case "reschedule_task":
                case "skip_task":
                    var matchedTask = TaskResolver.FindBestMatch(act.TaskRef ?? string.Empty, pendingTasks);
                    if (matchedTask == null)
                    {
                        continue; // No exact match found, ignore or could trigger clarification
                    }

                    if (act.Action == "complete_task")
                    {
                        matchedTask.Status = Data.Entities.TaskStatus.Completed;
                        matchedTask.CompletedAt = DateTimeOffset.Now;
                    }
                    else if (act.Action == "delete_task")
                    {
                        _db.Tasks.Remove(matchedTask);
                    }
                    else if (act.Action == "reschedule_task")
                    {
                        var updatedDate = ParseDate(act.NewDueDate);
                        if (updatedDate.HasValue) matchedTask.DueDate = updatedDate.Value;
                    }
                    else if (act.Action == "skip_task")
                    {
                        matchedTask.Status = Data.Entities.TaskStatus.Skipped;
                    }
                    break;

                case "create_recurring_task":
                    if (act.Title != null && act.Recurrence != null)
                    {
                        var template = new RecurringTemplate
                        {
                            Title = act.Title,
                            Recurrence = new RecurrenceRule
                            {
                                Frequency = act.Recurrence.Frequency,
                                Interval = act.Recurrence.Interval,
                                DaysOfWeek = act.Recurrence.DaysOfWeek
                            },
                            CreatedAt = DateTimeOffset.Now
                        };
                        _db.Templates.Add(template);
                        
                        // We also create the first instance for today so the user sees it immediately
                        _db.Tasks.Add(new TaskInstance
                        {
                            Title = act.Title,
                            DueDate = DateOnly.FromDateTime(DateTime.Now),
                            CreatedAt = DateTimeOffset.Now,
                            TemplateId = template.Id
                        }); 
                    }
                    break;

                case "clarify":
                    result.Success = false;
                    result.ClarificationQuestion = act.Question ?? "Could you please clarify?";
                    return result; // stop execution and ask
            }
        }

        await _db.SaveChangesAsync();
        return result;
    }

    private DateOnly? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        if (DateOnly.TryParse(dateStr, out var parsed)) return parsed;
        return null;
    }
}
