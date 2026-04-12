using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Security.Credentials;
using Sera.Pages;

namespace Sera;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
    }

    private void RootNavigationView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Check if API key exists
            if (!HasApiKey())
            {
                // Navigate to Settings and show notice
                RootNavigationView.SelectedItem = RootNavigationView.SettingsItem;
                NavigateToSettings(true);
            }
            else
            {
                // Navigate to Tasks by default
                var tasksItem = RootNavigationView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => i.Tag?.ToString() == "tasks");
                if (tasksItem != null)
                {
                    RootNavigationView.SelectedItem = tasksItem;
                    ContentFrame.Navigate(typeof(TasksPage));
                }
            }
        }
        catch (Exception)
        {
            // If something hangs during load, at least the window might show up blank
            ContentFrame.Navigate(typeof(TasksPage));
        }
    }

    private void RootNavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            NavigateToSettings(false);
        }
        else if (args.InvokedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag.ToString();
            if (tag == "tasks")
            {
                ContentFrame.Navigate(typeof(TasksPage));
            }
        }
    }

    private void NavigateToSettings(bool showNotice)
    {
        ContentFrame.Navigate(typeof(SettingsPage));
        if (showNotice && ContentFrame.Content is SettingsPage settingsPage)
        {
            settingsPage.ShowSetupNotice();
        }
    }

    private bool HasApiKey()
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve("SeraNvidia", "ApiKey");
            return !string.IsNullOrEmpty(credential.UserName);
        }
        catch
        {
            return false;
        }
    }
}
