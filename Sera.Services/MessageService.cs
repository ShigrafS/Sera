using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sera.Core.Models;
using Sera.Data;
using Sera.Data.Entities;
using Sera.Services.Execution;

namespace Sera.Services;

public interface IMessageService
{
    Task<ChatMessage> SaveMessageAsync(int conversationId, string role, string content);
    Task<IEnumerable<ChatMessage>> GetConversationMessagesAsync(int conversationId);
    Task UpdateMessageAsync(int messageId, string newContent);
}

public class MessageService : IMessageService
{
    private readonly SeraDbContext _db;

    public MessageService(SeraDbContext db)
    {
        _db = db;
    }

    public async Task<ChatMessage> SaveMessageAsync(int conversationId, string role, string content)
    {
        var message = new ChatMessage
        {
            ConversationId = conversationId,
            Role = role,
            Content = content,
            Timestamp = DateTimeOffset.Now
        };

        _db.ChatMessages.Add(message);

        var conversation = await _db.Conversations.FindAsync(conversationId);
        if (conversation != null)
        {
            conversation.UpdatedAt = DateTimeOffset.Now;
        }

        await _db.SaveChangesAsync();
        return message;
    }

    public async Task<IEnumerable<ChatMessage>> GetConversationMessagesAsync(int conversationId)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .ToListAsync();
        return messages.OrderBy(m => m.Timestamp);
    }

    public async Task UpdateMessageAsync(int messageId, string newContent)
    {
        var message = await _db.ChatMessages.FindAsync(messageId);
        if (message != null)
        {
            message.Content = newContent;
            await _db.SaveChangesAsync();
        }
    }
}
