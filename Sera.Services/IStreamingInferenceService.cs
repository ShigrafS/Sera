using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sera.Core.Models;
using Sera.Data.Entities;

namespace Sera.Services;

public interface IStreamingInferenceService
{
    IAsyncEnumerable<StreamingChunk> StreamResponseAsync(
        string userMessage,
        string contextDetails,
        IEnumerable<ChatMessage> conversationHistory,
        CancellationToken cancellationToken = default);
}

public class StreamingChunk
{
    public bool IsComplete { get; set; }
    public string? Token { get; set; }
    public ActionList? ParsedResponse { get; set; }
}
