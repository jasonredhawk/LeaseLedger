using LeaseLedger.Domain;

namespace LeaseLedger.Application;

public static class MaintenanceChat
{
    private static readonly string[] NavPrefixes =
    [
        "open ", "show me ", "show ", "go to ", "take me ", "pull up ", "bring up ", "navigate "
    ];

    private static readonly string[] FaultPhrases =
    [
        "something is wrong", "somethings wrong", "something wrong", "is wrong",
        "not working", "doesnt work", "does not work", "wont work", "isnt working", "stopped working",
        "no heat", "no hot water", "no power", "no water", "no electricity", "out of order",
        "needs repair", "needs fixing", "needs to be fixed", "wont turn on", "doesnt turn on", "does not turn on",
        "not heating", "not cooling", "backed up", "wont flush", "not flushing", "wont drain", "not draining",
        "keeps running", "wont close", "wont open", "wont lock", "doesnt lock", "does not lock",
        "will not latch", "wont latch", "will not close", "does not close", "doesnt close", "will not open", "will not lock",
        "fell apart", "came off", "is busted"
    ];

    private static readonly string[] FaultWords =
    [
        "broken", "broke", "leak", "leaking", "leaked", "mould", "mold", "clog", "clogged", "clogging",
        "damaged", "damage", "flood", "flooded", "flooding", "crack", "cracked", "cracking",
        "drip", "dripping", "stuck", "smell", "smelling", "smelly", "pest", "pests", "mice", "mouse",
        "bedbug", "bedbugs", "roach", "roaches", "cockroach", "busted", "overflow", "overflowing"
    ];

    private static readonly string[] MoneyWords =
    [
        "rent", "payment", "payments", "arrears", "receipt", "unpaid", "owing", "invoice"
    ];

    public static bool IsReport(string message)
    {
        var lower = Normalize(message);
        if (lower.Length == 0 || IsNotice(lower) || IsBarePage(lower))
            return false;
        if (StartsWithNav(lower) && !HasFault(lower))
            return false;
        if (IsMoney(lower) && !HasFault(lower))
            return false;

        return HasFault(lower) || IsWorkRequest(lower);
    }

    public static async Task<AssistantReply> ReplyAsync(
        ILeaseLedgerClient client,
        string message,
        IReadOnlyList<TenancySummaryDto> tenancies,
        Guid? contextTenancyId,
        CancellationToken cancellationToken = default)
    {
        var tenancy = Choose(message, tenancies, contextTenancyId);
        if (tenancy is null)
        {
            var text = tenancies.Count == 0 ? "Add a tenancy first." : "Which tenancy?";
            return new AssistantReply(text, null, false, contextTenancyId, null, null, null);
        }

        var problem = ProblemText(message, tenancy);
        var title = TodoAgent.TitleFrom(problem);
        if (title.Length == 0)
            title = "Maintenance request";
        if (title.Length > 160)
            title = title[..157].TrimEnd() + "...";

        var description = problem.Trim();
        if (description.Length > 4000)
            description = description[..4000];

        var open = await client.GetMaintenanceAsync(cancellationToken);
        var existing = open.FirstOrDefault(request =>
            request.TenancyId == tenancy.Id
            && request.Status != MaintenanceStatus.Completed
            && Overlaps(request.Title, title));
        if (existing is not null)
        {
            return Done(
                $"“{existing.Title}” is already open for {tenancy.TenantName}, unit {tenancy.UnitLabel}. It’s on the landlord's to-do list.",
                tenancy.Id);
        }

        var created = await client.CreateMaintenanceAsync(new CreateMaintenanceRequest
        {
            TenancyId = tenancy.Id,
            Title = title,
            Description = description
        }, cancellationToken);

        return Done(
            $"Opened maintenance “{created.Title}” for {tenancy.TenantName}, unit {tenancy.UnitLabel}. I added it to the landlord's to-do list.",
            tenancy.Id);
    }

    private static string ProblemText(string message, TenancySummaryDto tenancy)
    {
        var text = message.Trim();
        var name = tenancy.TenantName.Trim();
        var unit = tenancy.UnitLabel.Trim();
        var leads = new List<string>();
        if (name.Length > 0 && unit.Length > 0)
            leads.Add($"{name}, unit {unit}.");
        if (name.Length > 0)
            leads.Add(name + ".");

        foreach (var lead in leads)
        {
            if (!text.StartsWith(lead, StringComparison.OrdinalIgnoreCase))
                continue;

            var rest = text[lead.Length..].Trim();
            if (rest.Length > 0 && IsReport(rest))
                return rest;
        }

        return text;
    }

    private static AssistantReply Done(string text, Guid tenancyId) =>
        new(text, "/maintenance", false, tenancyId, "Open maintenance", null, null) { LedgerChanged = true };

    private static TenancySummaryDto? Choose(string message, IReadOnlyList<TenancySummaryDto> tenancies, Guid? contextTenancyId)
    {
        var matches = AssistantContext.Find(message, tenancies);
        if (matches.Count > 1)
            return null;
        if (matches.Count == 1)
            return matches[0];

        if (contextTenancyId is Guid id)
        {
            var context = tenancies.FirstOrDefault(tenancy => tenancy.Id == id);
            if (context is not null)
                return context;
        }

        return tenancies.Count == 1 ? tenancies[0] : null;
    }

    private static bool Overlaps(string left, string right)
    {
        var a = Collapse(left);
        var b = Collapse(right);
        return a.Length >= 8 && b.Length >= 8 && (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal));
    }

    private static string Collapse(string text)
    {
        var chars = Normalize(text).Select(character => char.IsLetterOrDigit(character) ? character : ' ').ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Normalize(string message)
    {
        var lower = message.Trim().ToLowerInvariant().Replace("'", "", StringComparison.Ordinal).Replace("’", "", StringComparison.Ordinal);
        return string.Join(' ', lower.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsBarePage(string lower)
    {
        var core = lower.TrimEnd('.', '!', '?').Trim();
        return core is "maintenance" or "repair" or "repairs" or "work order" or "work orders"
            or "payments" or "payment" or "home" or "dashboard" or "properties" or "property"
            or "forms" or "form" or "billing" or "legal" or "insurance" or "inspections" or "inspection"
            or "n1" or "n2" or "n3" or "n4" or "n5" or "n6" or "n7";
    }

    private static bool StartsWithNav(string lower) =>
        NavPrefixes.Any(prefix => lower.StartsWith(prefix, StringComparison.Ordinal));

    private static bool IsNotice(string lower) =>
        HasWord(lower, "notice") || Enumerable.Range(1, 7).Any(number => HasWord(lower, "n" + number));

    private static bool IsMoney(string lower) =>
        MoneyWords.Any(word => HasWord(lower, word))
        || lower.Contains("hasnt paid", StringComparison.Ordinal)
        || lower.Contains("has not paid", StringComparison.Ordinal)
        || lower.Contains("didnt pay", StringComparison.Ordinal)
        || lower.Contains("did not pay", StringComparison.Ordinal);

    private static bool HasFault(string lower) =>
        FaultPhrases.Any(phrase => ContainsPhrase(lower, phrase))
        || FaultWords.Any(word => HasWord(lower, word))
        || lower.Contains("plumb", StringComparison.Ordinal);

    private static bool IsWorkRequest(string lower)
    {
        if (StartsWithNav(lower))
            return false;

        var words = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 1 && words.Any(word => word is "fix" or "repair" or "replace");
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
}
