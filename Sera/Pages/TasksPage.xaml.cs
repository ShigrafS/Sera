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

public sealed partial class TasksPage : Page
{
    private readonly ObservableCollection<TaskInstance> _tasks = new();
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
        RefreshTasksAsync();
    }

    private async Task RefreshTasksAsync()
    {
        try
        {
            using var db = new SeraDbContext();
            var pendingTasks = await db.Tasks
                .Where(t => t.Status == Data.Entities.TaskStatus.Pending)
                .OrderBy(t => t.DueDate)
                .ToListAsync();

            _tasks.Clear();
            foreach (var t in pendingTasks)
            {
                _tasks.Add(t);
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error loading tasks: {ex.Message}";
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
            var context = string.Join(", ", _tasks.Select(t => t.Title));
            
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
}
