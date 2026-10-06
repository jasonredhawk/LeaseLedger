using System.Text;

namespace LeaseLedger.Application;

public sealed record AssistantPage(string Key, string Audience, string Purpose, string Fields);

public static class AssistantPages
{
    private const int MinYear = 2000;
    private const int MaxYear = 2100;

    [Flags]
    private enum Need
    {
        None = 0,
        Tenancy = 1,
        Unit = 2,
        Year = 4,
        Template = 8,
        Receipt = 16
    }

    private sealed record Page(string Key, string Audience, string Purpose, string Fields, Need Needs);

    private static readonly Page[] Pages =
    [
        new("home", "signed-in user", "Dashboard of arrears, open maintenance, and tenancies.", "none", Need.None),
        new("properties", "landlord", "Buildings, units, and the tenancies in them.", "none", Need.None),
        new("tenancy", "signed-in user", "One tenancy: rent, balance, and ledger.", "none", Need.Tenancy),
        new("payments", "landlord to record", "Payments and charges on a tenancy.", "payment: method (ETransfer, Cash, Cheque, Card, Other), amount, effectiveDate, note. charge: category (Rent, Other, Nsf), amount, effectiveDate, note", Need.Tenancy),
        new("n1", "landlord", "N1 rent increase within the guideline.", "noticeDate, proposedEffectiveDate", Need.Tenancy),
        new("n2", "landlord", "N2 rent increase for a unit exempt from the guideline.", "noticeDate, proposedEffectiveDate, proposedRent", Need.Tenancy),
        new("n3", "landlord", "N3 care home rent and meals.", "increasingRent, proposedRent, currentCareAndMeals, proposedCareAndMeals, noticeDate, effectiveDate", Need.Tenancy),
        new("n4", "landlord", "N4 notice to end a tenancy for non-payment.", "noticeDate", Need.Tenancy),
        new("n5", "landlord", "N5 notice for interference, damage, or overcrowding.", "noticeDate, reason (Interference, Damage, Overcrowding), secondNotice, firstNoticeDate, details", Need.Tenancy),
        new("n6", "landlord", "N6 notice for an illegal act.", "noticeDate, reason (DrugOffence, OtherIllegalAct, IncomeMisrepresentation), afterVoidableN5, details", Need.Tenancy),
        new("n7", "landlord", "N7 notice for a serious problem.", "noticeDate, details", Need.Tenancy),
        new("year-end", "signed-in user", "Year totals for a tenancy.", "year", Need.Tenancy | Need.Year),
        new("inspections", "landlord to record", "Inspection log for a unit.", "inspectedOn, summary, notes", Need.Unit),
        new("insurance", "landlord", "Tenant insurance record.", "status (Requested, Active, Cancelled), brokerName, policyNumber, landlordListed, note", Need.Tenancy),
        new("maintenance", "signed-in user", "Maintenance requests.", "tenancyId, title, description", Need.None),
        new("legal", "landlord", "Legal and paralegal contacts.", "none", Need.None),
        new("billing", "landlord", "Subscription plan.", "none", Need.None),
        new("forms", "landlord", "Uploaded PDF form templates.", "none", Need.None),
        new("form", "landlord", "One PDF template and its field map.", "templateId", Need.Template),
        new("receipt", "signed-in user", "A payment receipt.", "receiptNumber", Need.Receipt)
    ];

    public static IReadOnlyList<AssistantPage> All { get; } = Pages
        .Select(page => new AssistantPage(page.Key, page.Audience, page.Purpose, page.Fields))
        .ToArray();

    public static IReadOnlyList<string> Keys { get; } = Pages.Select(page => page.Key).ToArray();

    public static string Describe()
    {
        var text = new StringBuilder();
        foreach (var page in Pages)
        {
            text.Append(page.Key);
            text.Append(" (");
            text.Append(page.Audience);
            text.Append("): ");
            text.Append(page.Purpose);
            text.Append(" Fields: ");
            text.Append(page.Fields);
            text.AppendLine(".");
        }

        return text.ToString();
    }

    public static bool TryOpen(string? pageKey, AssistantRouteArgs args, out AssistantLink link)
    {
        link = null!;
        if (string.IsNullOrWhiteSpace(pageKey))
            return false;

        var page = Pages.FirstOrDefault(item => item.Key.Equals(pageKey.Trim(), StringComparison.OrdinalIgnoreCase));
        if (page is null)
            return false;

        if (page.Needs.HasFlag(Need.Tenancy) && !HasGuid(args.TenancyId))
            return false;
        if (page.Needs.HasFlag(Need.Unit) && !HasGuid(args.UnitId))
            return false;
        if (page.Needs.HasFlag(Need.Year))
        {
            if (args.Year is not int year || year < MinYear || year > MaxYear)
                return false;
        }
        if (page.Needs.HasFlag(Need.Template) && !HasGuid(args.TemplateId))
            return false;
        if (page.Needs.HasFlag(Need.Receipt) && !IsReceipt(args.ReceiptNumber))
            return false;

        var href = Href(page.Key, args);
        if (href is null)
            return false;

        var tenancyId = page.Needs.HasFlag(Need.Tenancy) ? args.TenancyId : null;
        link = new AssistantLink(href, Label(page.Key, args), tenancyId);
        return true;
    }

    private static string? Href(string key, AssistantRouteArgs args) => key switch
    {
        "home" => "/",
        "properties" => "/properties",
        "maintenance" => "/maintenance",
        "legal" => "/legal",
        "billing" => "/billing",
        "forms" => "/forms",
        "tenancy" => $"/tenancies/{args.TenancyId}",
        "payments" => $"/tenancies/{args.TenancyId}#payments",
        "insurance" => $"/tenancies/{args.TenancyId}/insurance",
        "n1" => $"/notices/n1/{args.TenancyId}",
        "n2" => $"/notices/n2/{args.TenancyId}",
        "n3" => $"/notices/n3/{args.TenancyId}",
        "n4" => $"/notices/n4/{args.TenancyId}",
        "n5" => $"/notices/n5/{args.TenancyId}",
        "n6" => $"/notices/n6/{args.TenancyId}",
        "n7" => $"/notices/n7/{args.TenancyId}",
        "year-end" => $"/tenancies/{args.TenancyId}/year/{args.Year}",
        "inspections" => $"/units/{args.UnitId}/inspections",
        "form" => $"/forms/{args.TemplateId}",
        "receipt" => $"/receipts/{args.ReceiptNumber}",
        _ => null
    };

    private static string Label(string key, AssistantRouteArgs args)
    {
        if (key.Length == 2 && key[0] == 'n' && char.IsDigit(key[1]))
            return "Open " + key.ToUpperInvariant();
        if (key == "year-end")
            return $"Open {args.Year} year-end";
        if (key == "home")
            return "Open home";
        return "Open " + key;
    }

    private static bool HasGuid(Guid? id) => id is Guid value && value != Guid.Empty;

    private static bool IsReceipt(string? receipt)
    {
        if (string.IsNullOrWhiteSpace(receipt) || receipt.Length > 32)
            return false;

        foreach (var character in receipt)
        {
            if (character is not ((>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
                return false;
        }

        return true;
    }
}
