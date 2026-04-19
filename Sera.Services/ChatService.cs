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

public interface IChatService
{
    Task<Conversation> CreateConversationAsync();
    Task<Conversation?> GetCurrentConversationAsync();
    Task<IEnumerable<Conversation>> GetAllConversationsAsync();
    Task DeleteConversationAsync(int conversationId);
    Task UpdateConversationTitleAsync(int conversationId, string title);
}

public class ChatService : IChatService
{
    private readonly SeraDbContext _db;

    public ChatService(SeraDbContext db)
    {
        _db = db;
    }

    public async Task<Conversation> CreateConversationAsync()
    {
        var conversation = new Conversation
        {
            Title = "New Chat",
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now
        };

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync();

        return conversation;
    }

    public async Task<Conversation?> GetCurrentConversationAsync()
    {
        var conversations = await _db.Conversations.ToListAsync();
        return conversations.OrderByDescending(c => c.UpdatedAt).FirstOrDefault();
    }

    public async Task<IEnumerable<Conversation>> GetAllConversationsAsync()
    {
        var conversations = await _db.Conversations.ToListAsync();
        return conversations.OrderByDescending(c => c.UpdatedAt);
    }

    public async Task DeleteConversationAsync(int conversationId)
    {
        var conversation = await _db.Conversations.FindAsync(conversationId);
        if (conversation != null)
        {
            _db.Conversations.Remove(conversation);
            await _db.SaveChangesAsync();
        }
    }

    public async Task UpdateConversationTitleAsync(int conversationId, string title)
    {
        var conversation = await _db.Conversations.FindAsync(conversationId);
        if (conversation != null)
        {
            conversation.Title = title;
            conversation.UpdatedAt = DateTimeOffset.Now;
            await _db.SaveChangesAsync();
        }
    }
}
