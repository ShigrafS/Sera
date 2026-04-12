using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Sera.Data;
using Sera.Data.Entities;

namespace Sera.Services;

[DisallowConcurrentExecution]
public class DailyRolloverJob : IJob
{
    private readonly SeraDbContext _db;

    public DailyRolloverJob(SeraDbContext db)
    {
        _db = db;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        
        // 1. Rollover: Move any pending ONE-TIME tasks that are overdue to today
        var overdueOneTimeTasks = await _db.Tasks
            .Where(t => t.Status == Data.Entities.TaskStatus.Pending 
                        && t.DueDate < today 
                        && t.TemplateId == null)
            .ToListAsync();

        foreach (var task in overdueOneTimeTasks)
        {
            task.DueDate = today;
        }

        // 2. Recurrence: Generate new instances for active templates
        var activeTemplates = await _db.Templates
            .Where(t => t.IsActive)
            .ToListAsync();

        foreach (var template in activeTemplates)
        {
            var rule = template.Recurrence;
            bool matchesToday = false;

            if (rule.Frequency.ToLowerInvariant() == "daily")
            {
                // check interval? MVP assume interval = 1
                matchesToday = true;
            }
            else if (rule.Frequency.ToLowerInvariant() == "weekly")
            {
                var currentDay = today.DayOfWeek.ToString().ToLowerInvariant();
                if (rule.DaysOfWeek.Contains(currentDay))
                {
                    matchesToday = true;
                }
            }

            if (matchesToday)
            {
                // ensure we haven't already generated one for today
                var existingForToday = await _db.Tasks
                    .AnyAsync(t => t.TemplateId == template.Id && t.DueDate == today);

                if (!existingForToday)
                {
                    _db.Tasks.Add(new TaskInstance
                    {
                        Title = template.Title,
                        DueDate = today,
                        Status = Data.Entities.TaskStatus.Pending,
                        TemplateId = template.Id,
                        CreatedAt = DateTimeOffset.Now
                    });
                }
            }
        }

        await _db.SaveChangesAsync();
    }
}
