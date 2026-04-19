using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Sera.Data;
using Sera.Services;
using Sera.Services.Execution;
using Sera.Core.Models;
using Sera.Controls;
using TaskStatus = Sera.Data.Entities.TaskStatus;
using TaskInstance = Sera.Data.Entities.TaskInstance;
using Conversation = Sera.Data.Entities.Conversation;
using ChatMessage = Sera.Data.Entities.ChatMessage;

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
    private readonly ObservableCollection<Conversation> _conversations = new();
    private readonly IStreamingInferenceService _streamingService;
    private readonly IActionExecutor _executor;
    private readonly HttpClient _httpClient = new();
    private readonly SeraDbContext _db;

    private Conversation? _currentConversation;
    private CancellationTokenSource? _streamingCts;

    public TasksPage()
    {
        this.InitializeComponent();

        _db = new SeraDbContext();
        _streamingService = new NvidiaStreamingInferenceService(_httpClient, _db);
        _executor = new ActionExecutor(_db);

        TasksList.ItemsSource = _tasks;
        ConversationsList.ItemsSource = _conversations;

        Loaded += TasksPage_Loaded;
    }

    private async void TasksPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("TasksPage_Loaded starting...");
            System.Diagnostics.Debug.WriteLine("Calling LoadConversationsAsync...");
            await LoadConversationsAsync();
            System.Diagnostics.Debug.WriteLine("LoadConversationsAsync completed");
            System.Diagnostics.Debug.WriteLine("Calling RefreshTasksAsync...");
            await RefreshTasksAsync();
            System.Diagnostics.Debug.WriteLine("RefreshTasksAsync completed");
            System.Diagnostics.Debug.WriteLine("TasksPage initialized successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing TasksPage: {ex}");
            StatusLabel.Text = $"Error loading data: {ex.Message}";
        }
    }

    private async Task InitializeAsync()
    {
        await LoadConversationsAsync();
        await RefreshTasksAsync();
    }

    private async Task LoadConversationsAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("LoadConversationsAsync: querying database...");
            var conversations = await _db.Conversations
                .ToListAsync();

            var sorted = conversations.OrderByDescending(c => c.UpdatedAt).ToList();
            System.Diagnostics.Debug.WriteLine($"LoadConversationsAsync: found {sorted.Count} conversations");

            _conversations.Clear();
            foreach (var c in sorted)
            {
                _conversations.Add(c);
            }

            if (_conversations.Any())
            {
                System.Diagnostics.Debug.WriteLine("LoadConversationsAsync: setting SelectedIndex to 0");
                ConversationsList.SelectedIndex = 0;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("LoadConversationsAsync: no conversations, creating new one");
                await CreateNewConversationAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadConversationsAsync error: {ex}");
            throw;
        }
    }

    private async Task CreateNewConversationAsync()
    {
        var conversation = new Conversation
        {
            Title = "New Chat",
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now
        };

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync();

        _conversations.Insert(0, conversation);
        ConversationsList.SelectedIndex = 0;
    }

    private async Task LoadConversationMessagesAsync(int conversationId)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        ConversationHistory.Children.Clear();

        foreach (var msg in messages)
        {
            AddMessageBubble(msg.Content, msg.Role == "user");
        }
    }

    private async Task RefreshTasksAsync()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            IQueryable<TaskInstance> query = _db.Tasks;

            switch (_currentTab)
            {
                case ViewTab.Today:
                    query = query.Where(t =>
                        t.DueDate == today ||
                        (t.Status == TaskStatus.Pending && t.DueDate < today));

                    var results = await query.OrderBy(t => t.DueDate).ToListAsync();
                    _tasks.Clear();
                    foreach (var t in results) _tasks.Add(t);
                    break;

                case ViewTab.Future:
                case ViewTab.Archive:
                    if (_currentTab == ViewTab.Future)
                        query = query.Where(t => t.DueDate > today);
                    else
                        query = query.Where(t => t.Status == TaskStatus.Completed && t.DueDate < today);

                    var groupedResults = await query.OrderBy(t => t.DueDate).ToListAsync();

                    var groups = groupedResults.GroupBy(t => t.DueDate)
                        .Select(g => new TaskGroup
                        {
                            Header = FormatDateHeader(g.Key),
                            Tasks = new ObservableCollection<TaskInstance>(g)
                        }).ToList();

                    if (groups.Any())
                    {
                        if (_currentTab == ViewTab.Future) groups.First().IsExpanded = true;
                        else groups.Last().IsExpanded = true;
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

    private async void NewConversation_Click(object sender, RoutedEventArgs e)
    {
        await CreateNewConversationAsync();
    }

    private async void ConversationsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConversationsList.SelectedItem is Conversation conversation)
        {
            _currentConversation = conversation;
            ConversationTitle.Text = conversation.Title;
            await LoadConversationMessagesAsync(conversation.Id);
        }
    }

    private void ChatInputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            System.Diagnostics.Debug.WriteLine("Enter key pressed, calling ProcessInputAsync");
            _ = ProcessInputAsync();
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Send button clicked, calling ProcessInputAsync");
            var task = ProcessInputAsync();
            System.Diagnostics.Debug.WriteLine($"ProcessInputAsync task created: {task.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception in SendButton_Click: {ex}");
        }
    }

    private async Task ProcessInputAsync()
    {
        var input = ChatInputBox.Text.Trim();
        System.Diagnostics.Debug.WriteLine($"ProcessInputAsync called with input: '{input}'");
        
        if (string.IsNullOrWhiteSpace(input))
        {
            System.Diagnostics.Debug.WriteLine("Input is empty, returning");
            return;
        }

        if (_currentConversation == null)
        {
            System.Diagnostics.Debug.WriteLine("_currentConversation is null, attempting to set...");
            if (ConversationsList.SelectedItem is Conversation conv)
            {
                _currentConversation = conv;
                System.Diagnostics.Debug.WriteLine($"Set _currentConversation from SelectedItem: {conv.Id}");
            }
            else if (_conversations.Any())
            {
                _currentConversation = _conversations.First();
                ConversationsList.SelectedItem = _currentConversation;
                System.Diagnostics.Debug.WriteLine($"Set _currentConversation from _conversations.First(): {_currentConversation.Id}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No conversations, creating new one...");
                await CreateNewConversationAsync();
            }
        }

        if (_currentConversation == null)
        {
            System.Diagnostics.Debug.WriteLine("_currentConversation is still null, returning");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"Proceeding with conversation {_currentConversation.Id}");
        SetUiLoading(true);
        ChatInputBox.Text = string.Empty;

        AddMessageBubble(input, true);
        await SaveMessageAsync(_currentConversation.Id, "user", input);

        var userMessage = new ChatMessage
        {
            ConversationId = _currentConversation.Id,
            Role = "user",
            Content = input,
            Timestamp = DateTimeOffset.Now
        };

        var assistantBubble = CreateAssistantBubble();
        ConversationHistory.Children.Add(assistantBubble);

        var streamingTextBlock = new StreamingTextBlock
        {
            IsStreaming = true
        };

        var bubbleContent = assistantBubble.Child as StackPanel;
        bubbleContent?.Children.Add(streamingTextBlock);

        StatusLabel.Text = "Sera is thinking...";

        try
        {
        var context = string.Join(", ", _tasks.Concat(_groupedTasks.SelectMany(g => g.Tasks)).Select(t => t.Title));
        var messages = await _db.ChatMessages
            .Where(m => m.ConversationId == _currentConversation.Id)
            .ToListAsync();
        var history = messages.OrderBy(m => m.Timestamp).ToList();

            _streamingCts = new CancellationTokenSource();
            ActionList? finalResponse = null;

            await foreach (var chunk in _streamingService.StreamResponseAsync(input, context, history, _streamingCts.Token))
            {
                if (chunk.IsComplete && chunk.ParsedResponse != null)
                {
                    finalResponse = chunk.ParsedResponse;
                }
                else if (!string.IsNullOrEmpty(chunk.Token))
                {
                    streamingTextBlock.AppendText(chunk.Token);
                }
            }

            if (finalResponse != null)
            {
                if (!string.IsNullOrEmpty(finalResponse.Message))
                {
                    streamingTextBlock.Text = finalResponse.Message;
                }

                await SaveMessageAsync(_currentConversation.Id, "assistant", finalResponse.Message);

                if (finalResponse.Actions.Any())
                {
                    var result = await _executor.ExecuteAsync(finalResponse.Actions);
                    if (result.Success)
                    {
                        await RefreshTasksAsync();
                        StatusLabel.Text = "Updated!";
                    }
                    else if (!string.IsNullOrEmpty(result.ClarificationQuestion))
                    {
                        streamingTextBlock.Text = result.ClarificationQuestion;
                    }
                }
            }

            _currentConversation.UpdatedAt = DateTimeOffset.Now;
            await _db.SaveChangesAsync();
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Something went wrong.";
            streamingTextBlock.Text = $"Error: {ex.Message}";
        }
        finally
        {
            streamingTextBlock.IsStreaming = false;
            SetUiLoading(false);
            _streamingCts?.Dispose();
            _streamingCts = null;
        }
    }

    private Border CreateAssistantBubble()
    {
        return new Border
        {
            Background = (Brush)Application.Current.Resources["SystemControlBackgroundBaseLowBrush"],
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new StackPanel()
        };
    }

    private void AddMessageBubble(string text, bool isUser)
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
                MaxWidth = 280,
                Foreground = isUser ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)) : (Brush)Application.Current.Resources["DefaultTextForegroundThemeBrush"]
            }
        };
        ConversationHistory.Children.Add(bubble);
        ScrollConversationToBottom();
    }

    private void ScrollConversationToBottom()
    {
        ConversationScrollViewer.UpdateLayout();
        ConversationScrollViewer.ScrollToVerticalOffset(ConversationScrollViewer.ScrollableHeight);
    }

    private async Task SaveMessageAsync(int conversationId, string role, string content)
    {
        var message = new ChatMessage
        {
            ConversationId = conversationId,
            Role = role,
            Content = content,
            Timestamp = DateTimeOffset.Now
        };

        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync();
    }

    private async void CompleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TaskInstance task })
        {
            try
            {
                var dbTask = await _db.Tasks.FindAsync(task.Id);
                if (dbTask != null)
                {
                    dbTask.Status = TaskStatus.Completed;
                    dbTask.CompletedAt = DateTimeOffset.Now;
                    await _db.SaveChangesAsync();
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
                var dbTask = await _db.Tasks.FindAsync(task.Id);
                if (dbTask != null)
                {
                    _db.Tasks.Remove(dbTask);
                    await _db.SaveChangesAsync();
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
        System.Diagnostics.Debug.WriteLine($"SetUiLoading called: {isLoading}");
        ChatInputBox.IsEnabled = !isLoading;
        SendButton.IsEnabled = !isLoading;
        if (!isLoading) 
        {
            ChatInputBox.Focus(FocusState.Programmatic);
            System.Diagnostics.Debug.WriteLine("Focus set to ChatInputBox");
        }
    }

    public static HorizontalAlignment GetMessageAlignment(string role)
    {
        return role == "user" ? HorizontalAlignment.Right : HorizontalAlignment.Left;
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

        var diff = (date.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days;
        if (diff > 1 && diff < 7) return date.ToString("dddd");

        return date.ToString("MMMM dd, yyyy");
    }
}
