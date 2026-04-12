using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Sera.Services;
using Sera.Services.Execution;

namespace Sera;

public sealed partial class MainWindow : Window
{
    // These would normally be constructor injected in App.xaml.cs via DI
    // private readonly INvidiaInferenceService _aiService;
    // private readonly IActionExecutor _executor;
    
    public MainWindow()
    {
        this.InitializeComponent();
        LoadTasksAsync();
    }

    private async void LoadTasksAsync()
    {
        // Dummy load call
        await Task.CompletedTask;
        // using var db = new SeraDbContext();
        // TasksList.ItemsSource = await db.Tasks.Where(t => t.Status == Data.Entities.TaskStatus.Pending).ToListAsync();
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
        
        ChatInputBox.IsEnabled = false;
        SendButton.IsEnabled = false;

        try
        {
            // Dummy logic to invoke the core loop
            await Task.Delay(100); 
            // var actions = await _aiService.ParseUserInputAsync(input, "some context");
            // if (actions != null) {
            //      await _executor.ExecuteAsync(actions.Actions);
            // }
            // LoadTasksAsync();
            ChatInputBox.Text = string.Empty;
        }
        catch (Exception)
        {
            // show error
        }
        finally
        {
            ChatInputBox.IsEnabled = true;
            SendButton.IsEnabled = true;
            ChatInputBox.Focus(FocusState.Programmatic);
        }
    }
}
