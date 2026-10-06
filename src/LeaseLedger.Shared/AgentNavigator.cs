using LeaseLedger.Application;
using LeaseLedger.Domain;

namespace LeaseLedger.Shared;

public static class AgentNavigator
{
    public sealed record Result(
        string Reply,
        string? Href,
        bool External,
        Guid? TenancyId,
        string? LinkLabel,
        bool Inspections);

    public static Result Interpret(string raw, IReadOnlyList<TenancySummaryDto> tenancies, Guid? contextTenancyId)
    {
        var text = raw.Trim().ToLowerInvariant();
        if (text.Length == 0)
            return Stay("Say which page to open.", contextTenancyId);

        var mentioned = MatchTenancies(text, tenancies);
        if (mentioned.Count > 1)
            return Stay("Which tenancy?", contextTenancyId);

        var chosen = mentioned.Count == 1
            ? mentioned[0]
            : tenancies.FirstOrDefault(tenancy => tenancy.Id == contextTenancyId);
        var tenancyId = chosen?.Id ?? contextTenancyId;

        if (MatchNotice(text) is string kind && Enum.TryParse<NoticeKind>(kind, true, out var noticeKind))
        {
            var form = OfficialForms.Find(noticeKind);
            return TenancyPage(chosen, tenancies, tenancyId, $"Opening the {form.Label}", tenancy => $"/notices/{form.Path}/{tenancy.Id}", $"Open {form.Label}");
        }

        if (HasWord(text, "increase") && HasWord(text, "rent"))
        {
            var n1 = OfficialForms.Find(NoticeKind.N1);
            return TenancyPage(chosen, tenancies, tenancyId, $"Opening the {n1.Label}", tenancy => $"/notices/{n1.Path}/{tenancy.Id}", $"Open {n1.Label}");
        }

        if (HasWord(text, "insurance"))
            return TenancyPage(chosen, tenancies, tenancyId, "Opening insurance", tenancy => $"/tenancies/{tenancy.Id}/insurance", "Open insurance");

        if (HasWord(text, "inspection") || HasWord(text, "inspections"))
            return TenancyPage(chosen, tenancies, tenancyId, "Opening inspections", _ => null, "Open inspections", inspections: true);

        if (HasWord(text, "year-end") || HasWord(text, "yearend") || ContainsPhrase(text, "year end"))
        {
            var year = ExtractYear(text) ?? DateTime.Now.Year;
            return TenancyPage(chosen, tenancies, tenancyId, $"Opening the {year} year-end", tenancy => $"/tenancies/{tenancy.Id}/year/{year}", $"Open {year} year-end");
        }

        if (HasWord(text, "map") || HasWord(text, "directions"))
        {
            var place = chosen ?? (tenancies.Count == 1 ? tenancies[0] : null);
            if (place is null)
                return NeedTenancy(tenancies, tenancyId);

            var query = string.Join(", ", new[] { place.PropertyStreet, place.City }.Where(part => !string.IsNullOrWhiteSpace(part)));
            var href = "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(query);
            return new Result($"Opening the map for {place.PropertyStreet}.", href, true, place.Id, "Open map", false);
        }

        if (HasAny(text, "pay", "payment", "payments", "transfer", "arrears", "receipt", "rent"))
            return TenancyPage(chosen, tenancies, tenancyId, "Opening payments", tenancy => $"/tenancies/{tenancy.Id}#payments", "Open payments");

        if (HasAny(text, "maintenance", "repair") || ContainsPhrase(text, "work order"))
            return Page("Opening maintenance.", "/maintenance", "Open maintenance", tenancyId);

        if (HasAny(text, "legal", "paralegal", "lawyer"))
            return Page("Opening legal contacts.", "/legal", "Open legal", tenancyId);

        if (HasAny(text, "billing", "plan", "subscription"))
            return Page("Opening billing.", "/billing", "Open billing", tenancyId);

        if (HasAny(text, "form", "forms", "template", "pdf"))
            return Page("Opening forms.", "/forms", "Open forms", tenancyId);

        if (HasWord(text, "properties") || HasWord(text, "property") || HasWord(text, "building") || HasWord(text, "buildings"))
            return Page("Opening properties.", "/properties", "Open properties", tenancyId);

        if (mentioned.Count == 0 && HasAny(text, "home", "dashboard"))
            return Page("Opening home.", "/", "Open home", tenancyId);

        if (mentioned.Count == 1 || HasAny(text, "tenancy", "lease", "ledger"))
            return TenancyPage(chosen, tenancies, tenancyId, "Opening the tenancy", tenancy => $"/tenancies/{tenancy.Id}", "Open tenancy");

        return Stay("Name a tenant or a page: payments, maintenance, insurance, inspections, forms, properties, legal, billing, or N1 through N7.", tenancyId);
    }

    private static Result Page(string reply, string href, string linkLabel, Guid? tenancyId) =>
        new(reply, href, false, tenancyId, linkLabel, false);

    private static Result Stay(string reply, Guid? tenancyId) =>
        new(reply, null, false, tenancyId, null, false);

    private static Result TenancyPage(
        TenancySummaryDto? chosen,
        IReadOnlyList<TenancySummaryDto> tenancies,
        Guid? contextId,
        string action,
        Func<TenancySummaryDto, string?> hrefFor,
        string linkLabel,
        bool inspections = false)
    {
        var tenancy = chosen ?? (tenancies.Count == 1 ? tenancies[0] : null);
        if (tenancy is null)
            return NeedTenancy(tenancies, contextId);

        return new Result(
            $"{action} for {tenancy.TenantName}, unit {tenancy.UnitLabel}.",
            hrefFor(tenancy),
            false,
            tenancy.Id,
            linkLabel,
            inspections);
    }

    private static Result NeedTenancy(IReadOnlyList<TenancySummaryDto> tenancies, Guid? contextId)
    {
        if (tenancies.Count == 0)
            return Stay("Add a tenancy first.", contextId);

        return Stay("Which tenancy?", contextId);
    }

    private static List<TenancySummaryDto> MatchTenancies(string text, IReadOnlyList<TenancySummaryDto> tenancies)
    {
        var hits = new List<(TenancySummaryDto Tenancy, int Score)>();
        foreach (var tenancy in tenancies)
        {
            var score = 0;
            if (ContainsPhrase(text, tenancy.TenantName.ToLowerInvariant()))
                score += 3;
            if (ContainsPhrase(text, tenancy.PropertyStreet.ToLowerInvariant()))
                score += 2;

            var unit = tenancy.UnitLabel.Trim().ToLowerInvariant();
            if (unit.Length > 0 && (ContainsPhrase(text, "unit " + unit) || (unit.Length >= 2 && ContainsPhrase(text, unit))))
                score += 2;

            if (score > 0)
                hits.Add((tenancy, score));
        }

        if (hits.Count == 0)
            return [];

        var best = hits.Max(hit => hit.Score);
        return hits.Where(hit => hit.Score == best).Select(hit => hit.Tenancy).ToList();
    }

    private static string? MatchNotice(string text)
    {
        for (var number = 1; number <= 7; number++)
        {
            var kind = "n" + number;
            if (ContainsPhrase(text, kind))
                return kind;
        }

        return null;
    }

    private static int? ExtractYear(string text)
    {
        for (var index = 0; index <= text.Length - 4; index++)
        {
            if (index > 0 && char.IsLetterOrDigit(text[index - 1]))
                continue;
            if (text[index] != '2' || text[index + 1] != '0' || !char.IsDigit(text[index + 2]) || !char.IsDigit(text[index + 3]))
                continue;
            if (index + 4 < text.Length && char.IsLetterOrDigit(text[index + 4]))
                continue;

            return int.Parse(text.AsSpan(index, 4));
        }

        return null;
    }

    private static bool HasWord(string text, string word) => ContainsPhrase(text, word);

    private static bool HasAny(string text, params string[] words) => words.Any(word => ContainsPhrase(text, word));

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
