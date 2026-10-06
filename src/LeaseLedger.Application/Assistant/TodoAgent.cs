using System.Globalization;
using System.Text.RegularExpressions;
using LeaseLedger.Domain;

namespace LeaseLedger.Application;

public enum TodoKind
{
    Tenant,
    Maintenance,
    Rent,
    Other
}

public sealed record TodoItem(
    string Key,
    TodoKind Kind,
    string Title,
    string? Detail,
    string? Href,
    Guid? TenancyId,
    Guid? ConversationId,
    bool FromLedger,
    DateOnly Due,
    bool Done,
    Guid? TurnId = null);

public sealed record TodoConversation(Guid Id, string Title, IReadOnlyList<TodoTurn> Turns);

public sealed record TodoTurn(string Role, string Text, string? PendingKey, string? PendingSummary, Guid? TurnId = null);

public sealed record TodoReply(string Label, string Send, string? Group = null);

public sealed record TodoCommandResult(
    string Text,
    IReadOnlyList<TodoItem> Items,
    IReadOnlyList<string>? RemovedKeys = null,
    IReadOnlyList<TodoReply>? Replies = null);

public static class TodoAgent
{
    private static readonly CultureInfo Canada = CultureInfo.GetCultureInfo("en-CA");
    private static readonly string[] TitlePrefixes =
    [
        "can you please ",
        "could you please ",
        "would you please ",
        "can you ",
        "could you ",
        "would you ",
        "please ",
        "add a task to ",
        "add a task ",
        "add task ",
        "add ",
        "to-do: ",
        "to-do ",
        "todo: ",
        "todo ",
        "remind me to ",
        "remind me ",
        "don't forget to ",
        "do not forget to ",
        "don't forget ",
        "i need to ",
        "we need to ",
        "i have to ",
        "we have to ",
        "i should ",
        "we should ",
        "you need to ",
        "you should ",
        "you'll need to ",
        "you will need to ",
        "make sure to ",
        "make sure ",
        "next step: ",
        "next step "
    ];

    private static readonly HashSet<string> Imperatives = new(StringComparer.Ordinal)
    {
        "call", "email", "text", "schedule", "book", "collect", "chase", "fix", "repair", "replace",
        "inspect", "serve", "send", "file", "contact", "visit", "order", "hire", "pay", "check",
        "review", "confirm", "update", "draft", "prepare", "notify", "ask", "follow"
    };

    public static readonly TodoKind[] KindOrder = [TodoKind.Tenant, TodoKind.Maintenance, TodoKind.Rent, TodoKind.Other];

    public static string KindLabel(TodoKind kind) => kind switch
    {
        TodoKind.Tenant => "Tenants",
        TodoKind.Maintenance => "Maintenance",
        TodoKind.Rent => "Rent",
        _ => "Other"
    };

    public static string KindClass(TodoKind kind) => "kind-" + kind.ToString().ToLowerInvariant();

    public static string ShortDate(DateOnly date, DateOnly today)
    {
        if (date == today)
            return "Today";
        if (date == today.AddDays(1))
            return "Tomorrow";
        if (date == today.AddDays(-1))
            return "Yesterday";

        var pattern = date.Year == today.Year ? "MMM d" : "MMM d, yyyy";
        return date.ToString(pattern, Canada);
    }

    public static IReadOnlyList<DateOnly> MonthDays(DateOnly month)
    {
        var first = new DateOnly(month.Year, month.Month, 1);
        var offset = ((int)first.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var start = first.AddDays(-offset);
        return Enumerable.Range(0, 42).Select(index => start.AddDays(index)).ToArray();
    }

    public static IReadOnlyList<TodoItem> Sync(
        IReadOnlyList<TenancySummaryDto> tenancies,
        IReadOnlyList<MaintenanceRequestDto> maintenance,
        IReadOnlyList<TodoConversation> conversations,
        IReadOnlyList<TodoItem> current,
        DateOnly today,
        DateOnly? nextDue)
    {
        ArgumentNullException.ThrowIfNull(tenancies);
        ArgumentNullException.ThrowIfNull(maintenance);
        ArgumentNullException.ThrowIfNull(conversations);
        ArgumentNullException.ThrowIfNull(current);

        var prior = current.ToDictionary(item => item.Key);
        var ledger = LedgerItems(tenancies, maintenance, today, nextDue);
        var fromChats = ConversationItems(conversations, tenancies, ledger, today);
        var merged = new List<TodoItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Take(TodoItem item)
        {
            if (!seen.Add(item.Key))
                return;

            if (prior.TryGetValue(item.Key, out var existing))
            {
                var due = item.FromLedger && item.Kind != TodoKind.Maintenance ? item.Due : existing.Due;
                item = item with { Done = existing.Done, Due = due };
            }

            merged.Add(item);
        }

        foreach (var item in ledger)
            Take(item);
        foreach (var item in fromChats)
            Take(item);
        foreach (var item in current.Where(item => item.Key.StartsWith("manual:", StringComparison.Ordinal)))
            Take(item);

        var manualBodies = merged
            .Where(item => item.Key.StartsWith("manual:", StringComparison.Ordinal))
            .Select(item => item.Key["manual:".Length..])
            .ToHashSet(StringComparer.Ordinal);

        merged.RemoveAll(item =>
            item.Key.StartsWith("chat:", StringComparison.Ordinal)
            && manualBodies.Contains(item.Key["chat:".Length..]));

        return Order(merged);
    }

    public static TodoCommandResult FindNew(
        IReadOnlyList<TodoItem> items,
        IReadOnlyList<TenancySummaryDto> tenancies,
        IReadOnlyList<MaintenanceRequestDto> maintenance,
        IReadOnlyList<TodoConversation> conversations,
        DateOnly today,
        DateOnly? nextDue)
    {
        ArgumentNullException.ThrowIfNull(items);
        var synced = Sync(tenancies, maintenance, conversations, items, today, nextDue);
        var present = items.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        var offered = synced
            .Where(item => !present.Contains(item.Key))
            .OrderBy(item => KindRank(item.Kind))
            .ThenBy(item => item.Due)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var replies = offered
            .Select(item => new TodoReply(
                $"{item.Title} · {ShortDate(item.Due, today)}",
                "add suggested " + item.Key,
                KindLabel(item.Kind)))
            .ToList();
        var text = offered.Count switch
        {
            0 => "No new items. The calendar already has everything I found.",
            1 => "This is not on the calendar yet. Tap it to add it.",
            _ => $"I found {offered.Count} items that are not on the calendar yet. Tap one to add it."
        };
        return new TodoCommandResult(text, items, Replies: replies);
    }

    public static TodoCommandResult Handle(
        string message,
        IReadOnlyList<TodoItem> items,
        IReadOnlyList<TenancySummaryDto> tenancies,
        IReadOnlyList<MaintenanceRequestDto> maintenance,
        IReadOnlyList<TodoConversation> conversations,
        DateOnly today,
        DateOnly? nextDue,
        DateOnly? defaultDue = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var command = CommandText(message);
        if (command.Length == 0)
            return new TodoCommandResult("Tell me a task, or ask me to review the chats.", items);

        if (TryTake(command, ["add suggested"], out var suggestedKey))
            return AcceptSuggested(items, suggestedKey, tenancies, maintenance, conversations, today, nextDue);

        if (IsFindNew(command))
            return FindNew(items, tenancies, maintenance, conversations, today, nextDue);

        if (IsReview(command))
        {
            var synced = Sync(tenancies, maintenance, conversations, items, today, nextDue);
            return new TodoCommandResult(Describe(synced, reviewed: true), synced);
        }

        if (IsList(command))
            return new TodoCommandResult(Describe(items, reviewed: false), items);

        if (TryTake(command, ["check off", "completed", "complete", "finish", "done"], out var doneQuery))
            return Mark(items, doneQuery, done: true);

        if (TryTake(command, ["not done", "reopen", "undo"], out var reopenQuery))
            return Mark(items, reopenQuery, done: false);

        if (TryTake(command, ["reschedule", "move"], out var moveQuery) && TrySplitMove(moveQuery, today, out var moveName, out var moveDue))
            return Reschedule(items, moveName, moveDue);

        if (TryTake(command, ["delete", "remove"], out var deleteQuery))
            return Delete(items, deleteQuery);

        if (TryTake(command, ["rename", "change", "update"], out var updateQuery) && updateQuery.Contains(" to ", StringComparison.Ordinal))
            return Update(items, updateQuery, today);

        var title = TaskTitle(message);
        var startedAsAdd = command.StartsWith("add ", StringComparison.Ordinal) || command is "add";
        if (title.Length == 0 || (!startedAsAdd && !IsUserTask(message, tenancies)))
            return new TodoCommandResult("I keep the to-do list. Add a task, rename or delete one, mark one done, or ask me to review the chats.", items);

        var (due, stripped) = StripDate(title, today);
        var explicitDate = due is not null;
        if (explicitDate && stripped.Length > 0)
            title = Capitalize(stripped);
        due ??= defaultDue ?? today;

        var tenancy = MatchTenancy(title, tenancies);
        var kind = Classify(title, tenancy);
        var item = new TodoItem(
            "manual:" + Normalize(title),
            kind,
            title,
            tenancy is null ? null : $"Unit {tenancy.UnitLabel}, {tenancy.PropertyStreet}",
            Href(kind, tenancy),
            tenancy?.Id,
            null,
            false,
            due.Value,
            false);

        var next = items.ToList();
        var body = item.Key["manual:".Length..];
        var existingIndex = next.FindIndex(candidate =>
            candidate.Key == item.Key || candidate.Key == "chat:" + body);
        if (existingIndex >= 0)
        {
            var existing = next[existingIndex];
            if (!existing.Done)
                return new TodoCommandResult($"“{existing.Title}” is already on the list.", items);

            next[existingIndex] = existing with { Done = false };
            return new TodoCommandResult($"Reopened “{existing.Title}”.", Order(next));
        }

        next.Add(item);
        var note = explicitDate || defaultDue is not null
            ? $"Added “{item.Title}” for {item.Due.ToString("MMMM d, yyyy", Canada)}."
            : $"Added “{item.Title}”.";
        return new TodoCommandResult(note, Order(next));
    }

    public static string Describe(IReadOnlyList<TodoItem> items, bool reviewed)
    {
        var open = items.Where(item => !item.Done).ToList();
        var lead = reviewed
            ? open.Count == 0 ? "Reviewed your chats. Nothing is open." : $"Reviewed your chats. {open.Count} open."
            : open.Count == 0 ? "Nothing is open." : $"{open.Count} open.";

        if (open.Count == 0)
            return lead;

        var lines = new List<string> { lead };
        foreach (var kind in new[] { TodoKind.Tenant, TodoKind.Maintenance, TodoKind.Rent, TodoKind.Other })
        {
            var group = open.Where(item => item.Kind == kind).ToList();
            if (group.Count == 0)
                continue;

            lines.Add($"{KindLabel(kind)}: {string.Join("; ", group.Select(item => item.Title))}.");
        }

        return string.Join("\n", lines);
    }

    private static List<TodoItem> LedgerItems(
        IReadOnlyList<TenancySummaryDto> tenancies,
        IReadOnlyList<MaintenanceRequestDto> maintenance,
        DateOnly today,
        DateOnly? nextDue)
    {
        var items = new List<TodoItem>();
        foreach (var tenancy in tenancies.OrderBy(item => item.TenantName, StringComparer.OrdinalIgnoreCase))
        {
            var unpaid = tenancy.RecentRent
                .Where(period => period.Timeliness == RentTimeliness.Unpaid)
                .OrderBy(period => period.DueOn)
                .ToList();
            if (unpaid.Count > 0 || tenancy.Balance > 0)
                items.Add(RentItem(tenancy, unpaid, today));

            var late = tenancy.RecentRent
                .Where(period => period.Timeliness == RentTimeliness.Late)
                .OrderByDescending(period => period.DueOn)
                .FirstOrDefault();
            if (late is not null)
                items.Add(LateItem(tenancy, late));
        }

        foreach (var request in maintenance.Where(request => request.Status != MaintenanceStatus.Completed).OrderBy(request => request.Title, StringComparer.OrdinalIgnoreCase))
        {
            var title = string.IsNullOrWhiteSpace(request.Title) ? "Maintenance request" : request.Title.Trim();
            items.Add(new TodoItem(
                $"maintenance:{request.Id:N}:{request.Status}",
                TodoKind.Maintenance,
                title,
                $"Unit {request.UnitLabel}, {request.PropertyStreet} · {StatusLabel(request.Status)}",
                "/maintenance",
                request.TenancyId,
                null,
                true,
                today,
                false));
        }

        if (nextDue is DateOnly due && due >= today && due <= today.AddDays(2))
        {
            items.Add(new TodoItem(
                $"rent:upcoming:{due:yyyy-MM-dd}",
                TodoKind.Rent,
                $"Rent is due {due.ToString("MMMM d, yyyy", Canada)}",
                "Check the tenancies before this date.",
                "/",
                null,
                null,
                true,
                due,
                false));
        }

        return items;
    }

    private static TodoItem RentItem(TenancySummaryDto tenancy, IReadOnlyList<RentPeriodDto> unpaid, DateOnly today)
    {
        var place = $"Unit {tenancy.UnitLabel}, {tenancy.PropertyStreet}";
        var owing = tenancy.Balance > 0 ? $"{Money(tenancy.Balance)} owing" : "Unpaid";
        var stamp = unpaid.Count > 0 ? unpaid[0].DueOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "other";
        string detail;
        if (unpaid.Count == 0)
            detail = $"{owing} · {place}";
        else if (unpaid.Count == 1)
            detail = $"Due {unpaid[0].DueOn.ToString("MMMM d, yyyy", Canada)} · {owing} · {place}";
        else
            detail = $"{unpaid.Count} unpaid periods, oldest {unpaid[0].DueOn.ToString("MMMM d, yyyy", Canada)} · {owing} · {place}";

        var title = unpaid.Count > 0
            ? $"Collect unpaid rent from {tenancy.TenantName}"
            : $"Collect {Money(tenancy.Balance)} from {tenancy.TenantName}";

        return new TodoItem(
            $"rent:{tenancy.Id:N}:{stamp}:{tenancy.Balance.ToString(CultureInfo.InvariantCulture)}",
            TodoKind.Rent,
            title,
            detail,
            $"/tenancies/{tenancy.Id}#payments",
            tenancy.Id,
            null,
            true,
            unpaid.Count > 0 ? unpaid[0].DueOn : today,
            false);
    }

    private static TodoItem LateItem(TenancySummaryDto tenancy, RentPeriodDto late) =>
        new(
            $"tenant:{tenancy.Id:N}:late:{late.DueOn:yyyy-MM-dd}",
            TodoKind.Tenant,
            $"Follow up with {tenancy.TenantName} on a late payment",
            $"Rent due {late.DueOn.ToString("MMMM d, yyyy", Canada)} was paid late · Unit {tenancy.UnitLabel}, {tenancy.PropertyStreet}",
            $"/tenancies/{tenancy.Id}",
            tenancy.Id,
            null,
            true,
            late.DueOn,
            false);

    private static List<TodoItem> ConversationItems(
        IReadOnlyList<TodoConversation> conversations,
        IReadOnlyList<TenancySummaryDto> tenancies,
        IReadOnlyList<TodoItem> ledger,
        DateOnly today)
    {
        var found = new List<TodoItem>();
        foreach (var conversation in conversations)
        {
            foreach (var turn in conversation.Turns)
            {
                if (!string.IsNullOrWhiteSpace(turn.PendingKey) && !string.IsNullOrWhiteSpace(turn.PendingSummary))
                {
                    AddChat(found, turn.PendingSummary, conversation, tenancies, ledger, today, turn.TurnId, $"proposal:{turn.PendingKey}", "Needs confirmation");
                    continue;
                }

                if (turn.Role == "you" && IsUserTask(turn.Text, tenancies))
                    AddChat(found, turn.Text, conversation, tenancies, ledger, today, turn.TurnId, null, null);
                else if (turn.Role == "agent" && IsAgentTask(turn.Text))
                    AddChat(found, turn.Text, conversation, tenancies, ledger, today, turn.TurnId, null, null);
            }
        }

        return found;
    }

    private static void AddChat(
        List<TodoItem> found,
        string text,
        TodoConversation conversation,
        IReadOnlyList<TenancySummaryDto> tenancies,
        IReadOnlyList<TodoItem> ledger,
        DateOnly today,
        Guid? turnId,
        string? key,
        string? confirmation)
    {
        var title = TaskTitle(text);
        if (title.Length == 0)
            return;

        var (due, stripped) = StripDate(title, today);
        if (due is not null && stripped.Length > 0)
            title = Capitalize(stripped);

        var fromProposal = key is not null;
        var tenancy = MatchTenancy(text + " " + title, tenancies);
        var kind = Classify(title, tenancy);
        key ??= "chat:" + Normalize(title);
        var from = conversation.Title is "New conversation" or "Voice" ? "From a chat" : $"From {conversation.Title}";
        var item = new TodoItem(
            key,
            kind,
            title,
            confirmation is null ? from : $"{confirmation} · {from}",
            Href(kind, tenancy),
            tenancy?.Id,
            conversation.Id,
            false,
            due ?? today,
            false,
            turnId);

        if (!fromProposal && CoveredByLedger(item, ledger))
            return;
        if (found.Any(existing => existing.Key == item.Key))
            return;

        found.Add(item);
    }

    private static bool CoveredByLedger(TodoItem item, IReadOnlyList<TodoItem> ledger)
    {
        if (item.Kind == TodoKind.Rent && item.TenancyId is Guid tenancyId)
            return ledger.Any(other => other.FromLedger && other.Kind == TodoKind.Rent && other.TenancyId == tenancyId);

        if (item.Kind != TodoKind.Maintenance)
            return false;

        return ledger.Any(other =>
            other.FromLedger
            && other.Kind == TodoKind.Maintenance
            && (item.TenancyId is null || other.TenancyId == item.TenancyId)
            && TitlesOverlap(item.Title, other.Title));
    }

    private static bool TitlesOverlap(string left, string right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        return a.Length > 0 && b.Length > 0 && (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal));
    }

    private static TodoCommandResult Mark(IReadOnlyList<TodoItem> items, string query, bool done)
    {
        var pool = items.Where(item => item.Done != done).ToList();
        var verb = done ? "done" : "reopen";
        if (query.Length == 0)
        {
            var ask = done ? "Which item should I check off?" : "Which item should I reopen?";
            return Choose(items, pool, ask, verb);
        }

        var kind = ParseKind(query);
        var matches = kind is TodoKind matched
            ? pool.Where(item => item.Kind == matched).ToList()
            : pool.Where(item => item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count == 0)
        {
            var missing = done ? "No open item matches that." : "No finished item matches that.";
            return new TodoCommandResult(missing, items);
        }

        if (matches.Count > 1 && kind is null)
            return Choose(items, matches, "Which one?", verb);

        var keys = matches.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        var next = items.Select(item => keys.Contains(item.Key) ? item with { Done = done } : item).ToList();
        var names = string.Join("; ", matches.Select(item => item.Title));
        var text = done
            ? matches.Count == 1 ? $"Checked off “{names}”." : $"Checked off {matches.Count}: {names}."
            : matches.Count == 1 ? $"Reopened “{names}”." : $"Reopened {matches.Count}: {names}.";
        return new TodoCommandResult(text, Order(next));
    }

    private static TodoCommandResult Delete(IReadOnlyList<TodoItem> items, string query)
    {
        if (query.Length == 0)
            return Choose(items, items, "Which item should I delete?", "delete");

        var kind = ParseKind(query);
        var matches = kind is TodoKind matched
            ? items.Where(item => item.Kind == matched).ToList()
            : items.Where(item => item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count == 0)
            return new TodoCommandResult("No item matches that.", items);

        if (matches.Count > 1 && kind is null)
            return Choose(items, matches, "Which one?", "delete");

        var keys = matches.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        var next = items.Where(item => !keys.Contains(item.Key)).ToList();
        var names = string.Join("; ", matches.Select(item => item.Title));
        var text = matches.Count == 1 ? $"Deleted “{names}”." : $"Deleted {matches.Count}: {names}.";
        return new TodoCommandResult(text, Order(next), keys.ToList());
    }

    private static TodoCommandResult Update(IReadOnlyList<TodoItem> items, string query, DateOnly today)
    {
        var parts = Regex.Split(query, @"\s+to\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[^1]))
            return new TodoCommandResult("Tell me the task and the new name or date, such as rename the plumber to email the broker.", items);

        var name = string.Join(" ", parts[..^1]).Trim();
        var replacement = parts[^1].Trim();
        var pool = items.Where(item => !item.Done && item.Title.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (pool.Count == 0)
            return new TodoCommandResult("No open item matches that.", items);
        if (pool.Count > 1)
            return Choose(items, pool, "Which one?", "rename", " to " + replacement);

        var (due, stripped) = StripDate(replacement, today);
        var title = stripped.Length > 0 ? Capitalize(stripped) : null;
        if (due is null && string.IsNullOrWhiteSpace(title))
            return new TodoCommandResult("Tell me a new name or a date.", items);

        var current = pool[0];
        var nextTitle = string.IsNullOrWhiteSpace(title) ? current.Title : title;
        var nextDue = due ?? current.Due;
        if (nextTitle == current.Title && nextDue == current.Due)
            return new TodoCommandResult($"“{current.Title}” is already set that way.", items);

        var next = items.Select(item => item.Key == current.Key ? item with { Title = nextTitle, Due = nextDue } : item).ToList();
        var note = nextTitle != current.Title && nextDue != current.Due
            ? $"Updated “{current.Title}” to “{nextTitle}” on {nextDue.ToString("MMMM d, yyyy", Canada)}."
            : nextTitle != current.Title
                ? $"Updated “{current.Title}” to “{nextTitle}”."
                : $"Updated “{current.Title}” to {nextDue.ToString("MMMM d, yyyy", Canada)}.";
        return new TodoCommandResult(note, Order(next));
    }

    private static TodoCommandResult Choose(
        IReadOnlyList<TodoItem> items,
        IReadOnlyList<TodoItem> matches,
        string prompt,
        string verb,
        string tail = "")
    {
        if (matches.Count == 0)
            return new TodoCommandResult(prompt, items);

        var replies = matches
            .Select(item => new TodoReply(item.Title, $"{verb} {item.Title}{tail}".Trim()))
            .ToList();
        return new TodoCommandResult(prompt, items, Replies: replies);
    }

    private static TodoCommandResult AcceptSuggested(
        IReadOnlyList<TodoItem> items,
        string key,
        IReadOnlyList<TenancySummaryDto> tenancies,
        IReadOnlyList<MaintenanceRequestDto> maintenance,
        IReadOnlyList<TodoConversation> conversations,
        DateOnly today,
        DateOnly? nextDue)
    {
        var wanted = key.Trim();
        if (wanted.Length == 0)
            return new TodoCommandResult("Tap one of the items to add it.", items);

        var existing = items.FirstOrDefault(item => item.Key.Equals(wanted, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return new TodoCommandResult($"“{existing.Title}” is already on the calendar.", items);

        var match = Sync(tenancies, maintenance, conversations, items, today, nextDue)
            .FirstOrDefault(item => item.Key.Equals(wanted, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return new TodoCommandResult("That item is no longer available.", items);

        return new TodoCommandResult(
            $"Added “{match.Title}” for {match.Due.ToString("MMMM d, yyyy", Canada)}.",
            Order(items.Append(match)));
    }

    public static string TitleFrom(string text) => TaskTitle(text);

    public static bool IsFindNewRequest(string message) => IsFindNew(CommandText(message));

    private static bool IsFindNew(string command) => command is
        "update" or "update list" or "update the list" or "update todos" or "update the todos"
        or "update to-dos" or "update the to-dos" or "update calendar" or "update the calendar"
        or "find new" or "find new items" or "find new tasks" or "find new todos" or "find new to-dos";

    private static bool IsReview(string command) => command is
        "review" or "review chats" or "review the chats" or "review conversations" or "review the conversations"
        or "scan" or "scan chats" or "scan the chats" or "check chats" or "check the chats" or "read the chats";

    private static bool IsList(string command) => command is
        "list" or "status" or "summary" or "what's open" or "whats open" or "what is open"
        or "show the list" or "show list";

    private static bool TryTake(string command, IReadOnlyList<string> prefixes, out string rest)
    {
        foreach (var prefix in prefixes.OrderByDescending(item => item.Length))
        {
            if (command.Equals(prefix, StringComparison.Ordinal))
            {
                rest = "";
                return true;
            }

            var spaced = prefix + " ";
            if (command.StartsWith(spaced, StringComparison.Ordinal))
            {
                rest = command[spaced.Length..].Trim();
                return true;
            }
        }

        rest = "";
        return false;
    }

    private static bool IsUserTask(string text, IReadOnlyList<TenancySummaryDto> tenancies)
    {
        var lower = StripPolite(text.Trim().ToLowerInvariant());
        if (lower.Length < 8)
            return false;
        if (tenancies.Any(tenancy => tenancy.TenantName.Equals(text.Trim(), StringComparison.OrdinalIgnoreCase)))
            return false;
        if (IsNavigation(lower) && !IsProblem(lower))
            return false;
        if (lower.EndsWith('?') && !HasCue(lower) && !IsProblem(lower))
            return false;

        return HasCue(lower) || IsProblem(lower) || StartsWithImperative(lower);
    }

    private static bool IsAgentTask(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        if (lower.Length < 12)
            return false;
        if (lower.StartsWith("opening", StringComparison.Ordinal)
            || lower.StartsWith("which tenancy", StringComparison.Ordinal)
            || lower.StartsWith("name a tenant", StringComparison.Ordinal)
            || lower.StartsWith("say which", StringComparison.Ordinal)
            || lower.StartsWith("that confirmation", StringComparison.Ordinal))
            return false;

        return lower.Contains("you should", StringComparison.Ordinal)
            || lower.Contains("you need to", StringComparison.Ordinal)
            || lower.Contains("you'll need", StringComparison.Ordinal)
            || lower.Contains("you will need", StringComparison.Ordinal)
            || lower.Contains("follow up", StringComparison.Ordinal)
            || lower.Contains("next step", StringComparison.Ordinal)
            || lower.Contains("still need", StringComparison.Ordinal)
            || lower.Contains("don't forget", StringComparison.Ordinal);
    }

    private static bool IsNavigation(string lower) =>
        lower is "home" or "payments" or "maintenance" or "properties" or "forms" or "billing" or "legal" or "insurance" or "inspections"
        || StartsWithAny(lower, "open ", "show me ", "show ", "go to ", "take me ", "pull up ", "bring up ", "navigate ");

    private static bool HasCue(string lower) =>
        lower.Contains("remind me", StringComparison.Ordinal)
        || lower.Contains("don't forget", StringComparison.Ordinal)
        || lower.Contains("do not forget", StringComparison.Ordinal)
        || lower.Contains("need to", StringComparison.Ordinal)
        || lower.Contains("have to", StringComparison.Ordinal)
        || lower.Contains("i should", StringComparison.Ordinal)
        || lower.Contains("we should", StringComparison.Ordinal)
        || lower.Contains("follow up", StringComparison.Ordinal)
        || lower.Contains("action item", StringComparison.Ordinal)
        || lower.Contains("to-do", StringComparison.Ordinal)
        || lower.Contains("todo:", StringComparison.Ordinal)
        || lower.Contains("make sure", StringComparison.Ordinal)
        || lower.Contains("hasn't paid", StringComparison.Ordinal)
        || lower.Contains("has not paid", StringComparison.Ordinal)
        || lower.Contains("didn't pay", StringComparison.Ordinal)
        || lower.Contains("did not pay", StringComparison.Ordinal)
        || lower.Contains("behind on", StringComparison.Ordinal);

    private static bool IsProblem(string lower) =>
        MaintenanceChat.IsReport(lower) || HasWord(lower, "arrears");

    private static bool StartsWithImperative(string lower)
    {
        var word = FirstWord(lower);
        return Imperatives.Contains(word);
    }

    private static TodoKind Classify(string title, TenancySummaryDto? tenancy)
    {
        var lower = title.ToLowerInvariant();
        if (IsMaintenance(lower))
            return TodoKind.Maintenance;
        if (IsRent(lower))
            return TodoKind.Rent;
        if (IsTenant(lower) || tenancy is not null)
            return TodoKind.Tenant;
        return TodoKind.Other;
    }

    private static bool IsMaintenance(string lower) =>
        MaintenanceChat.IsReport(lower)
        || HasWord(lower, "maintenance") || HasWord(lower, "repair") || HasWord(lower, "leak") || HasWord(lower, "leaking")
        || HasWord(lower, "broken") || lower.Contains("plumb", StringComparison.Ordinal)
        || ContainsPhrase(lower, "work order") || HasWord(lower, "mould") || HasWord(lower, "mold")
        || HasWord(lower, "toilet") || HasWord(lower, "faucet") || HasWord(lower, "tap");

    private static bool IsRent(string lower) =>
        HasWord(lower, "rent") || HasWord(lower, "arrears") || HasWord(lower, "payment") || HasWord(lower, "unpaid")
        || HasWord(lower, "owing") || HasWord(lower, "n4") || HasWord(lower, "receipt")
        || ContainsPhrase(lower, "hasn't paid") || ContainsPhrase(lower, "has not paid")
        || ContainsPhrase(lower, "didn't pay") || ContainsPhrase(lower, "did not pay")
        || ContainsPhrase(lower, "behind on");

    private static bool IsTenant(string lower) =>
        HasWord(lower, "tenant") || HasWord(lower, "lease") || HasWord(lower, "insurance") || HasWord(lower, "inspection")
        || HasWord(lower, "notice") || HasWord(lower, "n1") || HasWord(lower, "n2") || HasWord(lower, "n3")
        || HasWord(lower, "n5") || HasWord(lower, "n6") || HasWord(lower, "n7");

    private static TodoKind? ParseKind(string query) => query switch
    {
        "tenant" or "tenants" => TodoKind.Tenant,
        "maintenance" => TodoKind.Maintenance,
        "rent" => TodoKind.Rent,
        "other" => TodoKind.Other,
        _ => null
    };

    private static string? Href(TodoKind kind, TenancySummaryDto? tenancy) => kind switch
    {
        TodoKind.Maintenance => "/maintenance",
        TodoKind.Rent when tenancy is not null => $"/tenancies/{tenancy.Id}#payments",
        TodoKind.Tenant when tenancy is not null => $"/tenancies/{tenancy.Id}",
        TodoKind.Other when tenancy is not null => $"/tenancies/{tenancy.Id}",
        _ => null
    };

    private static TenancySummaryDto? MatchTenancy(string text, IReadOnlyList<TenancySummaryDto> tenancies)
    {
        var lower = text.ToLowerInvariant();
        var full = tenancies
            .Where(tenancy => tenancy.TenantName.Length >= 3 && ContainsPhrase(lower, tenancy.TenantName.ToLowerInvariant()))
            .ToList();
        if (full.Count == 1)
            return full[0];
        if (full.Count > 1)
            return null;

        var byFirst = tenancies
            .Select(tenancy => (Tenancy: tenancy, First: FirstWord(tenancy.TenantName.ToLowerInvariant())))
            .Where(pair => pair.First.Length >= 3 && ContainsPhrase(lower, pair.First))
            .GroupBy(pair => pair.First)
            .Where(group => group.Count() == 1)
            .Select(group => group.First().Tenancy)
            .ToList();
        return byFirst.Count == 1 ? byFirst[0] : null;
    }

    private static string TaskTitle(string text)
    {
        var trimmed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        trimmed = trimmed.Trim().TrimEnd('.', '!', '?').Trim();
        var lower = trimmed.ToLowerInvariant();
        var changed = true;
        while (changed && lower.Length > 0)
        {
            changed = false;
            foreach (var prefix in TitlePrefixes)
            {
                if (!lower.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                trimmed = trimmed[prefix.Length..].Trim();
                lower = trimmed.ToLowerInvariant();
                changed = true;
                break;
            }
        }

        trimmed = trimmed.TrimEnd('.', '!', '?').Trim();
        if (trimmed.Length == 0)
            return "";

        trimmed = char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
        return trimmed.Length <= 160 ? trimmed : trimmed[..157].TrimEnd() + "...";
    }

    private static string CommandText(string message)
    {
        var text = message.Trim().TrimEnd('.', '!', '?').Trim();
        return text.ToLowerInvariant();
    }

    private static string StripPolite(string lower)
    {
        var changed = true;
        while (changed && lower.Length > 0)
        {
            changed = false;
            foreach (var prefix in new[] { "can you please ", "could you please ", "would you please ", "can you ", "could you ", "would you ", "please " })
            {
                if (!lower.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                lower = lower[prefix.Length..].Trim();
                changed = true;
                break;
            }
        }

        return lower;
    }

    private static string Normalize(string text)
    {
        var chars = text.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : ' ').ToArray();
        var collapsed = string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= 80 ? collapsed : collapsed[..80];
    }

    private static TodoCommandResult Reschedule(IReadOnlyList<TodoItem> items, string name, DateOnly due)
    {
        var pool = items.Where(item => !item.Done && item.Title.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (pool.Count == 0)
            return new TodoCommandResult("No open item matches that.", items);
        if (pool.Count > 1)
            return Choose(items, pool, "Which one?", "move", " to " + due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var next = items.Select(item => item.Key == pool[0].Key ? item with { Due = due } : item).ToList();
        return new TodoCommandResult($"Moved “{pool[0].Title}” to {due.ToString("MMMM d, yyyy", Canada)}.", Order(next));
    }

    private static bool TrySplitMove(string query, DateOnly today, out string name, out DateOnly due)
    {
        name = "";
        due = default;
        var parts = Regex.Split(query, @"\s+(?:to|on|for)\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]))
            return false;

        var (found, _) = StripDate(parts[^1], today);
        if (found is null)
            return false;

        name = string.Join(" ", parts[..^1]).Trim();
        due = found.Value;
        return name.Length > 0;
    }

    private static (DateOnly? Due, string Title) StripDate(string title, DateOnly today)
    {
        var month = Regex.Match(
            title,
            @"\b(january|february|march|april|june|july|august|september|sept|october|november|december|jan|feb|mar|apr|jun|jul|aug|sep|oct|nov|dec|may)\.?\s+(\d{1,2})(?:st|nd|rd|th)?(?:,?\s+(\d{4}))?\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (month.Success && int.TryParse(month.Groups[2].Value, out var monthDay))
        {
            var monthNumber = MonthNumber(month.Groups[1].Value);
            var year = month.Groups[3].Success && int.TryParse(month.Groups[3].Value, out var given) ? given : today.Year;
            if (TryDate(year, monthNumber, monthDay, out var dated))
                return (dated, Cut(title, month));
        }

        var iso = Regex.Match(title, @"\b(\d{4})-(\d{2})-(\d{2})\b", RegexOptions.CultureInvariant);
        if (iso.Success && DateOnly.TryParseExact(iso.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var isoDate))
            return (isoDate, Cut(title, iso));

        var ordinal = Regex.Match(title, @"\b(?:on\s+)?the\s+(\d{1,2})(?:st|nd|rd|th)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!ordinal.Success)
            ordinal = Regex.Match(title, @"\bon\s+(\d{1,2})(?:st|nd|rd|th)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (ordinal.Success && int.TryParse(ordinal.Groups[1].Value, out var dayOfMonth))
        {
            var dated = dayOfMonth >= today.Day
                ? TryDay(today.Year, today.Month, dayOfMonth)
                : null;
            dated ??= TryDay(today.AddMonths(1).Year, today.AddMonths(1).Month, dayOfMonth);
            if (dated is DateOnly chosen)
                return (chosen, Cut(title, ordinal));
        }

        var week = Regex.Match(title, @"\bnext week\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (week.Success)
            return (today.AddDays(7), Cut(title, week));

        var relative = Regex.Match(title, @"\b(today|tomorrow|yesterday)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (relative.Success)
        {
            var dated = relative.Groups[1].Value.ToLowerInvariant() switch
            {
                "tomorrow" => today.AddDays(1),
                "yesterday" => today.AddDays(-1),
                _ => today
            };
            return (dated, Cut(title, relative));
        }

        var weekday = Regex.Match(
            title,
            @"\b(?:(next|this|on)\s+)?(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (weekday.Success && Enum.TryParse<DayOfWeek>(weekday.Groups[2].Value, ignoreCase: true, out var target))
        {
            var ahead = ((int)target - (int)today.DayOfWeek + 7) % 7;
            if (weekday.Groups[1].Value.Equals("next", StringComparison.OrdinalIgnoreCase))
                ahead = ahead == 0 ? 7 : ahead + 7;
            return (today.AddDays(ahead), Cut(title, weekday));
        }

        return (null, title);
    }

    private static DateOnly? TryDay(int year, int month, int day) =>
        TryDate(year, month, day, out var date) ? date : null;

    private static bool TryDate(int year, int month, int day, out DateOnly date)
    {
        try
        {
            date = new DateOnly(year, month, day);
            return month is >= 1 and <= 12;
        }
        catch (ArgumentOutOfRangeException)
        {
            date = default;
            return false;
        }
    }

    private static int MonthNumber(string name) => name.ToLowerInvariant().TrimEnd('.') switch
    {
        "jan" or "january" => 1,
        "feb" or "february" => 2,
        "mar" or "march" => 3,
        "apr" or "april" => 4,
        "may" => 5,
        "jun" or "june" => 6,
        "jul" or "july" => 7,
        "aug" or "august" => 8,
        "sep" or "sept" or "september" => 9,
        "oct" or "october" => 10,
        "nov" or "november" => 11,
        "dec" or "december" => 12,
        _ => 0
    };

    private static string Cut(string title, Match match)
    {
        var head = title[..match.Index].TrimEnd();
        var tail = title[(match.Index + match.Length)..].TrimStart();
        foreach (var word in new[] { "on", "by", "due", "for" })
        {
            if (head.EndsWith(" " + word, StringComparison.OrdinalIgnoreCase))
            {
                head = head[..^(word.Length + 1)].TrimEnd();
                break;
            }

            if (head.Equals(word, StringComparison.OrdinalIgnoreCase))
            {
                head = "";
                break;
            }
        }

        var combined = string.Join(' ', new[] { head, tail }.Where(part => part.Length > 0));
        return Regex.Replace(combined, @"\s+", " ").Trim(' ', ',', '-', '–');
    }

    private static string Capitalize(string text)
    {
        text = text.Trim();
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string Money(decimal amount) => amount.ToString("C", Canada);

    private static string StatusLabel(MaintenanceStatus status) => status switch
    {
        MaintenanceStatus.Submitted => "Submitted",
        MaintenanceStatus.Acknowledged => "Acknowledged",
        MaintenanceStatus.Scheduled => "Scheduled",
        _ => "Open"
    };

    private static IReadOnlyList<TodoItem> Order(IEnumerable<TodoItem> items) =>
        items
            .OrderBy(item => item.Done)
            .ThenBy(item => KindRank(item.Kind))
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static int KindRank(TodoKind kind)
    {
        var index = Array.IndexOf(KindOrder, kind);
        return index < 0 ? KindOrder.Length : index;
    }

    private static bool HasWord(string text, string word) => ContainsPhrase(text, word);

    private static bool ContainsPhrase(string text, string phrase)
    {
        var index = 0;
        while ((index = text.IndexOf(phrase, index, StringComparison.Ordinal)) >= 0)
        {
            var before = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var end = index + phrase.Length;
            var after = end >= text.Length || !char.IsLetterOrDigit(text[end]);
            if (before && after)
                return true;
            index = end;
        }

        return false;
    }

    private static bool StartsWithAny(string text, params string[] prefixes) =>
        prefixes.Any(prefix => text.StartsWith(prefix, StringComparison.Ordinal));

    private static string FirstWord(string text)
    {
        var space = text.IndexOf(' ');
        return space < 0 ? text : text[..space];
    }
}
