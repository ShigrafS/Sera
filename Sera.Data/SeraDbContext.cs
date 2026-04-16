using Microsoft.EntityFrameworkCore;
using Sera.Data.Entities;

namespace Sera.Data;

public class SeraDbContext : DbContext
{
    public DbSet<TaskInstance> Tasks { get; set; } = null!;
    public DbSet<RecurringTemplate> Templates { get; set; } = null!;
    public DbSet<AppSettings> Settings { get; set; } = null!;
    public DbSet<Conversation> Conversations { get; set; } = null!;
    public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
    
    // Default constructor for EF Core tools
    public SeraDbContext()
    {
        Database.EnsureCreated();
    }

    public SeraDbContext(DbContextOptions<SeraDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sera", "tasks.db");
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

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(m => new { m.ConversationId, m.Timestamp });

        modelBuilder.Entity<Conversation>()
            .HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

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
