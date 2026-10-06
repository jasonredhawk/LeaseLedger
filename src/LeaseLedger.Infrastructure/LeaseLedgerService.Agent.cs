using System.Text.Json;
using LeaseLedger.Application;
using LeaseLedger.Domain;
using Microsoft.EntityFrameworkCore;

namespace LeaseLedger.Infrastructure;

public sealed partial class LeaseLedgerService
{
    private const int MaxConversations = 40;
    private const int MaxTurns = 80;
    private const int MaxActions = 8;
    private const int MaxNotes = 80;
    private const int MaxOverrides = 200;
    private const int MaxTodos = 200;
    private const int MaxPayload = 500_000;

    public async Task<AgentHistoryDto> GetAgentHistoryAsync(CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);
        var row = await db.AgentHistories.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == user.UserId, cancellationToken);
        if (row is null || string.IsNullOrWhiteSpace(row.Payload))
            return AgentHistoryDto.Empty;

        try
        {
            var stored = JsonSerializer.Deserialize<AgentHistoryDto>(row.Payload, LeaseLedgerJson.Options);
            return Normalize(stored);
        }
        catch (JsonException)
        {
            return AgentHistoryDto.Empty;
        }
    }

    public async Task SaveAgentHistoryAsync(AgentHistoryDto history, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);
        var cleaned = Normalize(history);
        var json = JsonSerializer.Serialize(cleaned, LeaseLedgerJson.Options);
        if (json.Length > MaxPayload)
            throw new LeaseLedgerException("That chat history is too large.");

        var userId = user.UserId!.Value;
        var row = await db.AgentHistories.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (row is null)
        {
            db.AgentHistories.Add(new AgentHistory
            {
                UserId = userId,
                Payload = json,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            row.Payload = json;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static AgentHistoryDto Normalize(AgentHistoryDto? history)
    {
        var conversations = (history?.Conversations ?? [])
            .GroupBy(conversation => conversation.Id)
            .Select(group => NormalizeConversation(group.Last()))
            .Where(conversation => conversation.Turns.Count > 0)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .Take(MaxConversations)
            .ToList();

        var notes = (history?.Notes ?? [])
            .Where(note => note.Role is "you" or "agent" && !string.IsNullOrWhiteSpace(note.Text))
            .TakeLast(MaxNotes)
            .Select(NormalizeNote)
            .ToList();

        var overrides = (history?.Overrides ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var item = group.Last();
                return item with
                {
                    Key = Clip(item.Key, 200),
                    Title = item.Title is null ? null : Clip(item.Title, 200)
                };
            })
            .Take(MaxOverrides)
            .ToList();

        var todos = (history?.Todos ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Title))
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var item = group.Last();
                return item with
                {
                    Key = Clip(item.Key, 200),
                    Title = Clip(item.Title, 200),
                    Detail = item.Detail is null ? null : Clip(item.Detail, 300),
                    Href = item.Href is null ? null : Clip(item.Href, 500)
                };
            })
            .Take(MaxTodos)
            .ToList();

        return new AgentHistoryDto(conversations, notes, overrides, todos);
    }

    private static AgentConversationDto NormalizeConversation(AgentConversationDto conversation)
    {
        var turns = (conversation.Turns ?? [])
            .Where(turn => turn.Role is "you" or "agent")
            .TakeLast(MaxTurns)
            .Select(NormalizeTurn)
            .ToList();

        return conversation with
        {
            Title = Clip(string.IsNullOrWhiteSpace(conversation.Title) ? "New conversation" : conversation.Title, 80),
            Preview = conversation.Preview is null ? null : Clip(conversation.Preview, 280),
            LastHref = conversation.LastHref is null ? null : Clip(conversation.LastHref, 500),
            Turns = turns
        };
    }

    private static AgentNoteDto NormalizeNote(AgentNoteDto note)
    {
        var replies = (note.Replies ?? [])
            .Where(reply => !string.IsNullOrWhiteSpace(reply.Label) && !string.IsNullOrWhiteSpace(reply.Send))
            .Take(6)
            .Select(reply => reply with
            {
                Label = Clip(reply.Label, 80),
                Send = Clip(reply.Send, 500)
            })
            .ToList();

        return note with
        {
            Text = Clip(note.Text, 4000),
            Replies = replies
        };
    }

    private static AgentTurnDto NormalizeTurn(AgentTurnDto turn)
    {
        var actions = (turn.Actions ?? [])
            .Where(action => !string.IsNullOrWhiteSpace(action.Href) && !string.IsNullOrWhiteSpace(action.Label))
            .Take(MaxActions)
            .Select(action => action with
            {
                Href = Clip(action.Href, 500),
                Label = Clip(action.Label, 120),
                Group = action.Group is null ? null : Clip(action.Group, 80),
                Send = action.Send is null ? null : Clip(action.Send, 500)
            })
            .ToList();

        return turn with
        {
            Text = Clip(turn.Text, 8000),
            ProposalSummary = turn.ProposalSummary is null ? null : Clip(turn.ProposalSummary, 1000),
            Actions = actions
        };
    }

    private static string Clip(string value, int max)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
