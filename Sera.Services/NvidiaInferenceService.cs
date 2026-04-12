using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sera.Data;
using Sera.Core.Models;
using Windows.Security.Credentials;

namespace Sera.Services;

public interface INvidiaInferenceService
{
    Task<ActionList?> ParseUserInputAsync(string userMessage, string contextDetails);
}

public class NvidiaInferenceService : INvidiaInferenceService
{
    private readonly HttpClient _httpClient;
    private readonly SeraDbContext _db;

    public NvidiaInferenceService(HttpClient httpClient, SeraDbContext db)
    {
        _httpClient = httpClient;
        _db = db;
    }

    private string? GetApiKey()
    {
        try
        {
            var vault = new PasswordVault();
            // We use "SeraNvidia" as the resource name, "ApiKey" as the user name
            var credential = vault.Retrieve("SeraNvidia", "ApiKey");
            credential.RetrievePassword();
            return credential.Password;
        }
        catch
        {
            return null; // Not found
        }
    }

    public async Task<ActionList?> ParseUserInputAsync(string userMessage, string contextDetails)
    {
        var settings = await _db.Settings.FirstOrDefaultAsync() ?? new Sera.Data.Entities.AppSettings();
        string apiKey = GetApiKey() ?? "";

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new Exception("NVIDIA API Key not configured. Please set it in Settings.");
        }

        var systemPrompt = @"You are a task extraction engine. You receive user intent and extract deterministic JSON actions.
Output STRICTLY a JSON object with this schema:
{
  ""actions"": [
    {
      ""action"": ""add_task"" | ""complete_task"" | ""delete_task"" | ""reschedule_task"" | ""skip_task"" | ""create_recurring_task"" | ""clarify"",
      ""task_ref"": ""<name of task if modifying existing>"",
      ""title"": ""<title if new>"",
      ""newDueDate"": ""<yyyy-MM-dd if rescheduling>"",
      ""recurrence"": { ""frequency"": ""daily|weekly"", ""interval"": 1, ""days"": [] },
      ""question"": ""<question if clarify>""
    }
  ]
}
If unclear, output action: clarify and question.";

        var requestBody = new
        {
            model = settings.SelectedModel,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"Context:\n{contextDetails}\n\nUser: {userMessage}" }
            },
            temperature = 0.2, // enforce low temperature for deterministic parsing
            max_tokens = 250,  // small max tokens to prevent unbounded generation
            response_format = new { type = "json_object" }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl}/chat/completions")
        {
            Content = content
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _httpClient.SendAsync(requestMessage);
        
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        
        using var jsonDoc = JsonDocument.Parse(responseString);
        var messageContent = jsonDoc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (messageContent == null) return null;

        return JsonSerializer.Deserialize<ActionList>(messageContent);
    }
}
