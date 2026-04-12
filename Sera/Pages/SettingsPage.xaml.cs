using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Sera.Data;
using Sera.Data.Entities;
using Windows.Security.Credentials;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Sera.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly PasswordVault _vault = new();
    private const string VaultResource = "SeraNvidia";
    private const string VaultUser = "ApiKey";

    public SettingsPage()
    {
        this.InitializeComponent();
        LoadSettings();
    }

    private async void LoadSettings()
    {
        try
        {
            // Load API Key from Vault
            try
            {
                var credentials = _vault.Retrieve(VaultResource, VaultUser);
                credentials.RetrievePassword();
                ApiKeyBox.Password = credentials.Password;
            }
            catch { /* Not found */ }

            // Load other settings from DB
            using var db = new SeraDbContext();
            var settings = await db.Settings.FirstOrDefaultAsync();
            if (settings != null)
            {
                // Find and select the model in ComboBox
                foreach (ComboBoxItem item in ModelSelector.Items)
                {
                    if (item.Content.ToString() == settings.SelectedModel)
                    {
                        ModelSelector.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Fail silently or show error
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Save API Key to Vault
            if (!string.IsNullOrWhiteSpace(ApiKeyBox.Password))
            {
                _vault.Add(new PasswordCredential(VaultResource, VaultUser, ApiKeyBox.Password));
            }

            // Save other settings to DB
            using var db = new SeraDbContext();
            var settings = await db.Settings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new AppSettings();
                db.Settings.Add(settings);
            }

            if (ModelSelector.SelectedItem is ComboBoxItem selectedItem)
            {
                settings.SelectedModel = selectedItem.Content.ToString()!;
            }
            
            settings.LastUpdated = DateTimeOffset.Now;
            await db.SaveChangesAsync();

            // Notify user
            var dialog = new ContentDialog
            {
                Title = "Success",
                Content = "Settings saved successfully.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = $"Failed to save settings: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        TestResultText.Visibility = Visibility.Visible;
        TestResultText.Text = "Testing connection...";
        TestResultText.Foreground = (Brush)Application.Current.Resources["DefaultTextForegroundThemeBrush"];

        try
        {
            var key = ApiKeyBox.Password;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new Exception("Please enter an API key first.");
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            
            // Minimalist test call to models endpoint
            var response = await client.GetAsync("https://integrate.api.nvidia.com/v1/models");
            
            if (response.IsSuccessStatusCode)
            {
                TestResultText.Text = "Success! Connection to NVIDIA API established.";
                TestResultText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0));
            }
            else
            {
                TestResultText.Text = $"Failed: {response.ReasonPhrase}";
                TestResultText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 0, 0));
            }
        }
        catch (Exception ex)
        {
            TestResultText.Text = $"Error: {ex.Message}";
            TestResultText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 0, 0));
        }
    }

    public void ShowSetupNotice()
    {
        SetupInfoBar.IsOpen = true;
    }
}
