namespace LeaseLedger.Domain;

public sealed record OfficialForm(NoticeKind Kind, string Title)
{
    public string Label => $"{Kind} – {Title}";

    public string Path => Kind.ToString().ToLowerInvariant();
}

public static class OfficialForms
{
    public static IReadOnlyList<OfficialForm> All { get; } =
    [
        new(NoticeKind.N1, "Notice of Rent Increase"),
        new(NoticeKind.N2, "Notice of Rent Increase (Unit Partially Exempt)"),
        new(NoticeKind.N3, "Notice to Increase the Rent and/or Charges for Care Services and Meals"),
        new(NoticeKind.N4, "Notice to End your Tenancy Early for Non-payment of Rent"),
        new(NoticeKind.N5, "Notice to End your Tenancy for Interfering with Others, Damage or Overcrowding"),
        new(NoticeKind.N6, "Notice to End your Tenancy for Illegal Acts or Misrepresenting Income in a Rent-Geared-to-Income Rental Unit"),
        new(NoticeKind.N7, "Notice to End your Tenancy for Causing Serious Problems in the Rental Unit or Residential Complex")
    ];

    public static OfficialForm Find(NoticeKind kind) =>
        All.First(form => form.Kind == kind);
}
