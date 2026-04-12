using Microsoft.EntityFrameworkCore;
using TaskSage.Data.Entities;

namespace TaskSage.Data;

public class TaskSageDbContext : DbContext
{
    public DbSet<TaskInstance> Tasks { get; set; } = null!;
    public DbSet<RecurringTemplate> Templates { get; set; } = null!;
    public DbSet<AppSettings> Settings { get; set; } = null!;
    
    // Default constructor for EF Core tools
    public TaskSageDbContext()
    {
    }

    public TaskSageDbContext(DbContextOptions<TaskSageDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskSage", "tasks.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskInstance>()
            .HasIndex(t => new { t.DueDate, t.Status });
            
        modelBuilder.Entity<TaskInstance>()
            .HasIndex(t => t.TemplateId);
            
        // Setup initial default singleton settings
        modelBuilder.Entity<AppSettings>().HasData(new AppSettings
        {
            Id = 1,
            SelectedModel = "meta/llama-3.1-70b-instruct",
            BaseUrl = "https://integrate.api.nvidia.com/v1",
            LastUpdated = DateTimeOffset.Now
        });
    }
}
