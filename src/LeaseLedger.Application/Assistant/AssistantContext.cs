namespace LeaseLedger.Application;

public static class AssistantContext
{
    private static readonly string[] PageWords =
    [
        "pay", "payment", "payments", "transfer", "arrears", "receipt", "rent",
        "insurance", "inspection", "inspections", "maintenance", "repair",
        "legal", "paralegal", "lawyer", "billing", "plan", "subscription",
        "form", "forms", "template", "pdf", "properties", "property", "building", "buildings",
        "home", "dashboard", "tenancy", "lease", "ledger", "map", "directions", "increase",
        "year", "yearend", "n1", "n2", "n3", "n4", "n5", "n6", "n7"
    ];

    private static readonly string[] NameStops = ["tenant", "tenants", "someone", "anybody", "anyone"];

    private static readonly (string Key, string Label)[] PersonPages =
    [
        ("tenancy", "Ledger"),
        ("payments", "Payments"),
        ("insurance", "Insurance"),
        ("inspections", "Inspections"),
        ("year-end", "Year-end"),
        ("n1", "N1"),
        ("n4", "N4"),
        ("n5", "N5")
    ];

    public static AssistantReply Enrich(
        AssistantReply reply,
        string message,
        IReadOnlyList<TenancySummaryDto> tenancies,
        IReadOnlyDictionary<Guid, Guid> unitIds,
        int year,
        IReadOnlyList<PdfTemplateSummaryDto>? templates = null)
    {
        if (reply.LedgerChanged)
            return reply;

        templates ??= [];
        var matches = Find(message, tenancies);
        if (matches.Count == 1 && DocumentKind(message) is not null)
            return Documents(reply, matches[0], unitIds, year, templates);

        if (NeedsChoice(message, matches, tenancies))
        {
            var pool = matches.Count > 1 ? matches : tenancies;
            return reply with
            {
                Text = ChoiceQuestion(reply.Text),
                Href = null,
                External = false,
                LinkLabel = null,
                Actions = Choices(pool, message)
            };
        }

        if (matches.Count == 0)
            return reply;

        var links = new List<AssistantLink>();
        var groupMatches = matches.Count > 1;
        foreach (var tenancy in matches)
        {
            unitIds.TryGetValue(tenancy.Id, out var unitId);
            var group = groupMatches ? $"{tenancy.TenantName} · unit {tenancy.UnitLabel}" : null;
            links.AddRange(LinksFor(tenancy, unitId == Guid.Empty ? null : unitId, year, group));
        }

        if (links.Count == 0)
            return reply;

        if (HasPageIntent(message))
            return reply with { Actions = links, TenancyId = matches.Count == 1 ? matches[0].Id : reply.TenancyId };

        var text = matches.Count == 1
            ? $"{matches[0].TenantName} is in unit {matches[0].UnitLabel} at {matches[0].PropertyStreet}. Choose a page."
            : $"I found {matches.Count} matches. Choose a page for each.";

        return reply with
        {
            Text = text,
            Href = null,
            External = false,
            LinkLabel = null,
            TenancyId = matches.Count == 1 ? matches[0].Id : reply.TenancyId,
            Actions = links
        };
    }

    private static AssistantReply Documents(
        AssistantReply reply,
        TenancySummaryDto tenancy,
        IReadOnlyDictionary<Guid, Guid> unitIds,
        int year,
        IReadOnlyList<PdfTemplateSummaryDto> templates)
    {
        var links = new List<AssistantLink>();
        var agreements = templates.Where(IsAgreementTemplate).ToList();
        if (agreements.Count == 0)
            links.Add(new AssistantLink("/forms", "Rent agreement", tenancy.Id, "Documents"));
        else
        {
            foreach (var template in agreements)
                links.Add(new AssistantLink($"/forms/{template.Id}", template.Name, tenancy.Id, "Documents"));
        }

        unitIds.TryGetValue(tenancy.Id, out var unitId);
        links.AddRange(LinksFor(tenancy, unitId == Guid.Empty ? null : unitId, year, tenancy.TenantName));

        var lead = $"{tenancy.TenantName}'s rent agreement, tenancy, and related documents are below.";
        var text = string.IsNullOrWhiteSpace(reply.Text) || reply.Text.StartsWith("Opening", StringComparison.Ordinal)
            ? lead
            : reply.Text.Trim() + "\n\n" + lead;

        var href = $"/tenancies/{tenancy.Id}";
        if (AssistantPages.TryOpen("tenancy", new AssistantRouteArgs(tenancy.Id, null, null, null, null), out var details))
            href = details.Href;

        links.Insert(agreements.Count == 0 ? 1 : agreements.Count, new AssistantLink(href, "Tenancy details", tenancy.Id, tenancy.TenantName));
        return reply with
        {
            Text = text,
            Href = href,
            External = false,
            LinkLabel = "Tenancy details",
            TenancyId = tenancy.Id,
            Actions = links
        };
    }

    private static string ChoiceQuestion(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return "Which tenancy?";

        var head = trimmed.Split('\n')[0].Trim();
        if (head.StartsWith("Which tenancy", StringComparison.OrdinalIgnoreCase)
            || head.StartsWith("Which tenant", StringComparison.OrdinalIgnoreCase)
            || head.StartsWith("Which one", StringComparison.OrdinalIgnoreCase))
            return "Which tenancy?";

        return trimmed;
    }

    private static List<AssistantLink> Choices(IReadOnlyList<TenancySummaryDto> pool, string message)
    {
        var links = new List<AssistantLink>();
        var asked = message.Trim();
        foreach (var tenancy in pool)
        {
            var label = TenancyChoiceLabel(tenancy);
            links.Add(new AssistantLink("", label, tenancy.Id, "Tenants", ReplyFor(label, asked)));
        }

        foreach (var unit in pool.GroupBy(tenancy => tenancy.UnitLabel.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var members = unit.ToList();
            var only = members.Count == 1 ? members[0] : null;
            var label = string.IsNullOrEmpty(unit.Key) ? "Unit" : "Unit " + unit.Key;
            var send = only is null ? ReplyFor("unit " + unit.Key, asked) : ReplyFor(TenancyChoiceLabel(only), asked);
            links.Add(new AssistantLink("", label, only?.Id, "Units", send));
        }

        return links;
    }

    private static string TenancyChoiceLabel(TenancySummaryDto tenancy)
    {
        var unit = tenancy.UnitLabel.Trim();
        return unit.Length == 0 ? tenancy.TenantName : $"{tenancy.TenantName}, unit {unit}";
    }

    private static string ReplyFor(string choice, string asked)
    {
        if (asked.Length == 0 || asked.Equals(choice, StringComparison.OrdinalIgnoreCase))
            return choice;

        return $"{choice}. {asked}";
    }

    private static bool NeedsChoice(string message, IReadOnlyList<TenancySummaryDto> matches, IReadOnlyList<TenancySummaryDto> tenancies)
    {
        if (tenancies.Count < 2 || matches.Count == 1)
            return false;
        if (matches.Count > 1)
            return true;

        return IsPersonTopic(message);
    }

    private static bool IsPersonTopic(string message)
    {
        var text = message.Trim().ToLowerInvariant();
        return MaintenanceChat.IsReport(message)
            || ContainsPhrase(text, "tenant") || ContainsPhrase(text, "tenancy") || ContainsPhrase(text, "rent")
            || ContainsPhrase(text, "late") || ContainsPhrase(text, "arrears") || ContainsPhrase(text, "payment")
            || ContainsPhrase(text, "lease") || ContainsPhrase(text, "agreement") || ContainsPhrase(text, "notice")
            || ContainsPhrase(text, "insurance") || ContainsPhrase(text, "inspection") || ContainsPhrase(text, "increase")
            || ContainsPhrase(text, "owe") || ContainsPhrase(text, "owing")
            || Enumerable.Range(1, 7).Any(number => ContainsPhrase(text, "n" + number));
    }

    private static string? DocumentKind(string message)
    {
        var text = message.Trim().ToLowerInvariant();
        if (ContainsPhrase(text, "agreement") || ContainsPhrase(text, "contract") || ContainsPhrase(text, "addendum")
            || ContainsPhrase(text, "standard lease"))
            return "rent agreement";

        return null;
    }

    private static bool IsAgreementTemplate(PdfTemplateSummaryDto template)
    {
        if (template.NoticeKind is not null)
            return false;

        var hay = $"{template.Name} {template.FileName}".ToLowerInvariant();
        return hay.Contains("agreement", StringComparison.Ordinal)
            || hay.Contains("lease", StringComparison.Ordinal)
            || hay.Contains("contract", StringComparison.Ordinal);
    }

    public static IReadOnlyList<TenancySummaryDto> Find(string message, IReadOnlyList<TenancySummaryDto> tenancies)
    {
        var text = message.Trim().ToLowerInvariant();
        var words = Words(text).Where(word => word.Length >= 3 && !PageWords.Contains(word) && !NameStops.Contains(word)).ToList();
        var hits = new List<(TenancySummaryDto Tenancy, int Score)>();
        foreach (var tenancy in tenancies)
        {
            var score = 0;
            var name = tenancy.TenantName.ToLowerInvariant();
            var street = tenancy.PropertyStreet.ToLowerInvariant();
            var unit = tenancy.UnitLabel.Trim().ToLowerInvariant();
            foreach (var word in words)
            {
                if (ContainsPhrase(name, word))
                    score += 3;
                if (ContainsPhrase(street, word))
                    score += 2;
            }

            if (unit.Length > 0 && (ContainsPhrase(text, "unit " + unit) || (unit.Length >= 2 && words.Contains(unit))))
                score += 2;

            if (score > 0)
                hits.Add((tenancy, score));
        }

        if (hits.Count == 0)
            return [];

        var best = hits.Max(hit => hit.Score);
        return hits.Where(hit => hit.Score == best).Select(hit => hit.Tenancy).ToList();
    }

    private static bool HasPageIntent(string message)
    {
        var text = message.Trim().ToLowerInvariant();
        return Words(text).Any(word => PageWords.Contains(word));
    }

    private static List<AssistantLink> LinksFor(TenancySummaryDto tenancy, Guid? unitId, int year, string? group)
    {
        var links = new List<AssistantLink>();
        foreach (var (key, label) in PersonPages)
        {
            var args = new AssistantRouteArgs(
                tenancy.Id,
                key == "inspections" ? unitId : null,
                key == "year-end" ? year : null,
                null,
                null);
            if (!AssistantPages.TryOpen(key, args, out var opened))
                continue;

            links.Add(opened with { Label = label, Group = group, TenancyId = tenancy.Id });
        }

        return links;
    }

    private static List<string> Words(string text)
    {
        var words = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Append(character);
                continue;
            }

            if (current.Length == 0)
                continue;

            words.Add(current.ToString());
            current.Clear();
        }

        if (current.Length > 0)
            words.Add(current.ToString());

        return words;
    }

    private static bool ContainsPhrase(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return false;

        var index = 0;
        while ((index = text.IndexOf(phrase, index, StringComparison.Ordinal)) >= 0)
        {
            var before = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var afterIndex = index + phrase.Length;
            var after = afterIndex >= text.Length || !char.IsLetterOrDigit(text[afterIndex]);
            if (before && after)
                return true;

            index += phrase.Length;
        }

        return false;
    }
}
