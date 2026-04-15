using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.EntityFrameworkCore;
using Sera.Data;
using Sera.Data.Entities;
using Sera.Services;
using Sera.Services.Execution;

namespace Sera.Pages;

public class TaskGroup
{
    public string Header { get; set; } = string.Empty;
    public ObservableCollection<TaskInstance> Tasks { get; set; } = new();
    public bool IsExpanded { get; set; } = false;
}

public sealed partial class TasksPage : Page
{
    private enum ViewTab { Today, Future, Archive }
    private ViewTab _currentTab = ViewTab.Today;

    private readonly ObservableCollection<TaskInstance> _tasks = new();
    private readonly ObservableCollection<TaskGroup> _groupedTasks = new();
    private readonly INvidiaInferenceService _aiService;
    private readonly IActionExecutor _executor;
    private readonly HttpClient _httpClient = new();

    public TasksPage()
    {
        this.InitializeComponent();
        
        // Manual instantiation (Pragmatic approach)
        var db = new SeraDbContext();
        _aiService = new NvidiaInferenceService(_httpClient, db);
        _executor = new ActionExecutor(db);
        
        TasksList.ItemsSource = _tasks;
        _ = RefreshTasksAsync();
    }

    private async Task RefreshTasksAsync()
    {
        try
        {
            using var db = new SeraDbContext();
            var today = DateOnly.FromDateTime(DateTime.Today);
            
            IQueryable<TaskInstance> query = db.Tasks;

            switch (_currentTab)
            {
                case ViewTab.Today:
                    query = query.Where(t => 
                        t.DueDate == today || 
                        (t.Status == Data.Entities.TaskStatus.Pending && t.DueDate < today));
                    
                    var results = await query.OrderBy(t => t.DueDate).ToListAsync();
                    _tasks.Clear();
                    foreach (var t in results) _tasks.Add(t);
                    break;

                case ViewTab.Future:
                case ViewTab.Archive:
                    if (_currentTab == ViewTab.Future)
                        query = query.Where(t => t.DueDate > today);
                    else
                        query = query.Where(t => t.Status == Data.Entities.TaskStatus.Completed && t.DueDate < today);

                    var groupedResults = await query.OrderBy(t => t.DueDate).ToListAsync();
                    
                    // Grouping logic
                    var groups = groupedResults.GroupBy(t => t.DueDate)
                        .Select(g => new TaskGroup 
                        { 
                            Header = FormatDateHeader(g.Key), 
                            Tasks = new ObservableCollection<TaskInstance>(g) 
                        }).ToList();

                    // Expand the nearest date
                    if (groups.Any())
                    {
                        if (_currentTab == ViewTab.Future) groups.First().IsExpanded = true;
                        else groups.Last().IsExpanded = true; // Most recent for archive
                    }

                    _groupedTasks.Clear();
                    foreach (var g in groups) _groupedTasks.Add(g);
                    break;
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error loading tasks: {ex.Message}";
        }
    }
    
    private void UpdateViewVisibility()
    {
        if (_currentTab == ViewTab.Today)
        {
            TasksList.Visibility = Visibility.Visible;
            GroupedTasksContainer.Visibility = Visibility.Collapsed;
        }
        else
        {
            TasksList.Visibility = Visibility.Collapsed;
            GroupedTasksContainer.Visibility = Visibility.Visible;
        }
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            // Reset styles
            TodayTab.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
            FutureTab.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
            ArchiveTab.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
            
            TodayTab.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
            FutureTab.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
            ArchiveTab.FontWeight = Microsoft.UI.Text.FontWeights.Normal;

            if (btn == TodayTab) _currentTab = ViewTab.Today;
            else if (btn == FutureTab) _currentTab = ViewTab.Future;
            else if (btn == ArchiveTab) _currentTab = ViewTab.Archive;

            btn.Foreground = (Brush)Application.Current.Resources["AccentAAFillColorDefaultBrush"];
            btn.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;

            UpdateViewVisibility();
            _ = RefreshTasksAsync();
        }
    }

    private void ChatInputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            ProcessInput();
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        ProcessInput();
    }

    private async void ProcessInput()
    {
        var input = ChatInputBox.Text;
        if (string.IsNullOrWhiteSpace(input)) return;

        SetUiLoading(true);
        StatusLabel.Text = "Sera is thinking...";
        AddConversationBubble(input, true);

        try
        {
            // Gather context for AI (titles of pending tasks)
            var context = string.Join(", ", _tasks.Concat(_groupedTasks.SelectMany(g => g.Tasks)).Select(t => t.Title));
            
            var actionList = await _aiService.ParseUserInputAsync(input, context);
            
            if (actionList == null || actionList.Actions == null || !actionList.Actions.Any())
            {
                AddConversationBubble("I couldn't quite understand that. Could you try rephrasing?", false);
                return;
            }

            var result = await _executor.ExecuteAsync(actionList.Actions);
            
            if (!result.Success && !string.IsNullOrEmpty(result.ClarificationQuestion))
            {
                AddConversationBubble(result.ClarificationQuestion, false);
            }
            else
            {
                await RefreshTasksAsync();
                StatusLabel.Text = "Updated!";
                ChatInputBox.Text = string.Empty;
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Something went wrong.";
            AddConversationBubble($"Error: {ex.Message}", false);
        }
        finally
        {
            SetUiLoading(false);
        }
    }

    private async void CompleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TaskInstance task })
        {
            try
            {
                using var db = new SeraDbContext();
                var dbTask = await db.Tasks.FindAsync(task.Id);
                if (dbTask != null)
                {
                    dbTask.Status = Data.Entities.TaskStatus.Completed;
                    dbTask.CompletedAt = DateTimeOffset.Now;
                    await db.SaveChangesAsync();
                    await RefreshTasksAsync();
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
            }
        }
    }

    private async void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TaskInstance task })
        {
            try
            {
                using var db = new SeraDbContext();
                var dbTask = await db.Tasks.FindAsync(task.Id);
                if (dbTask != null)
                {
                    db.Tasks.Remove(dbTask);
                    await db.SaveChangesAsync();
                    await RefreshTasksAsync();
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
            }
        }
    }

    private void SetUiLoading(bool isLoading)
    {
        ChatInputBox.IsEnabled = !isLoading;
        SendButton.IsEnabled = !isLoading;
        if (!isLoading) ChatInputBox.Focus(FocusState.Programmatic);
    }

    private void AddConversationBubble(string text, bool isUser)
    {
        var bubble = new Border
        {
            Background = (Brush)Application.Current.Resources[isUser ? "AccentAAFillColorDefaultBrush" : "SystemControlBackgroundBaseLowBrush"],
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Child = new TextBlock 
            { 
                Text = text, 
                TextWrapping = TextWrapping.Wrap, 
                MaxWidth = 250,
                Foreground = isUser ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)) : (Brush)Application.Current.Resources["DefaultTextForegroundThemeBrush"]
            }
        };
        ConversationHistory.Children.Add(bubble);
        
        // Auto-scroll to bottom would be nice, but simple for now
    }

    public static Visibility GetCompleteButtonVisibility(Data.Entities.TaskStatus status)
    {
        return status == Data.Entities.TaskStatus.Pending ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Windows.UI.Text.TextDecorations GetTextDecoration(Data.Entities.TaskStatus status)
    {
        return status == Data.Entities.TaskStatus.Completed ? Windows.UI.Text.TextDecorations.Strikethrough : Windows.UI.Text.TextDecorations.None;
    }

    private string FormatDateHeader(DateOnly date)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (date == today) return "Today";
        if (date == today.AddDays(1)) return "Tomorrow";
        if (date == today.AddDays(-1)) return "Yesterday";
        
        // Show day name for the next 6 days
        var diff = (date.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days;
        if (diff > 1 && diff < 7) return date.ToString("dddd");
        
        return date.ToString("MMMM dd, yyyy");
    }
}
