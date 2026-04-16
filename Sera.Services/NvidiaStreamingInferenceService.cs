using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sera.Core.Models;
using Sera.Data;
using Sera.Data.Entities;
using Windows.Security.Credentials;

namespace Sera.Services;

public class NvidiaStreamingInferenceService : IStreamingInferenceService
{
    private readonly HttpClient _httpClient;
    private readonly SeraDbContext _db;

    public NvidiaStreamingInferenceService(HttpClient httpClient, SeraDbContext db)
    {
        _httpClient = httpClient;
        _db = db;
    }

    private string? GetApiKey()
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve("SeraNvidia", "ApiKey");
            credential.RetrievePassword();
            return credential.Password;
        }
        catch
        {
            return null;
        }
    }

    private string BuildSystemPrompt()
    {
        return $@"You are Sera, a friendly AI task assistant. Your name is Sera. Always respond in first person as Sera.

The current date is {DateTime.Now:yyyy-MM-dd}. Use this date to resolve relative time references like 'tomorrow', 'next week', etc.

Your role is to help users manage their tasks through natural conversation. When users mention tasks, extract them as JSON actions AND provide a brief, friendly confirmation message.

Output STRICTLY a JSON object with this schema:
{{
    ""actions"": [
        {{
            ""action"": ""add_task"" | ""complete_task"" | ""delete_task"" | ""reschedule_task"" | ""skip_task"" | ""create_recurring_task"" | ""clarify"",
            ""task_ref"": ""<name of task if modifying existing>"",
            ""title"": ""<title if new>"",
            ""dueDate"": ""<yyyy-MM-dd>"",
            ""recurrence"": {{ ""frequency"": ""daily|weekly"", ""interval"": 1, ""days"": [] }},
            ""question"": ""<question if clarify>""
        }}
    ],
    ""message"": ""<Your conversational response as Sera, friendly and concise>""
}}

Guidelines:
- Be warm and helpful, but concise
- When you successfully add or modify a task, briefly confirm it
- If the user's intent is unclear, ask a clarifying question
- Always speak as Sera, using 'I' and 'me'
- The message field should feel natural, like talking to a helpful assistant";
    }

    private List<object> BuildMessages(string userMessage, string contextDetails, IEnumerable<ChatMessage> conversationHistory)
    {
        var messages = new List<object>
        {
            new { role = "system", content = BuildSystemPrompt() }
        };

        foreach (var msg in conversationHistory.OrderBy(m => m.Timestamp))
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }

        var userContent = string.IsNullOrEmpty(contextDetails)
            ? userMessage
            : $"Context:\n{contextDetails}\n\nUser: {userMessage}";

        messages.Add(new { role = "user", content = userContent });

        return messages;
    }

    public async IAsyncEnumerable<StreamingChunk> StreamResponseAsync(
        string userMessage,
        string contextDetails,
        IEnumerable<ChatMessage> conversationHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = await _db.Settings.FirstOrDefaultAsync() ?? new AppSettings();
        var apiKey = GetApiKey();

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("NVIDIA API Key not configured. Please set it in Settings.");
        }

        var messages = BuildMessages(userMessage, contextDetails, conversationHistory);

        var requestBody = new
        {
            model = settings.SelectedModel,
            messages = messages,
            temperature = 0.3,
            max_tokens = 500,
            stream = true,
            response_format = new { type = "json_object" }
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var fullContent = new StringBuilder();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrEmpty(line))
                continue;

            if (!line.StartsWith("data: "))
                continue;

            var data = line.Substring(6);

            if (data == "[DONE]")
            {
                var parsed = ParseFullResponse(fullContent.ToString());
                yield return new StreamingChunk
                {
                    IsComplete = true,
                    ParsedResponse = parsed
                };
                yield break;
            }

            var parsedJson = TryParseDelta(data);
            if (parsedJson != null)
            {
                fullContent.Append(parsedJson);
                yield return new StreamingChunk
                {
                    IsComplete = false,
                    Token = parsedJson
                };
            }
        }

        // Final fallback if stream didn't end with [DONE]
        if (fullContent.Length > 0)
        {
            var parsed = ParseFullResponse(fullContent.ToString());
            yield return new StreamingChunk
            {
                IsComplete = true,
                ParsedResponse = parsed
            };
        }
    }

    private string? TryParseDelta(string data)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(data);
            var delta = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("delta");

            if (delta.TryGetProperty("content", out var contentProp))
            {
                return contentProp.GetString() ?? string.Empty;
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private ActionList? ParseFullResponse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ActionList>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return new ActionList
            {
                Actions = new List<ActionCommand>(),
                Message = "I had trouble processing that. Could you try again?"
            };
        }
    }
}
