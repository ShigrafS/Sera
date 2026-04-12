using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Sera.Services;
using Sera.Services.Execution;

namespace Sera.Pages;

public sealed partial class TasksPage : Page
{
    // private readonly INvidiaInferenceService _aiService;
    // private readonly IActionExecutor _executor;
    
    public TasksPage()
    {
        this.InitializeComponent();
        LoadTasksAsync();
    }

    private async void LoadTasksAsync()
    {
        // Dummy load call
        await Task.CompletedTask;
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
            await Task.Delay(100); 
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
