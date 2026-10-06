using LeaseLedger.Domain;

namespace LeaseLedger.Domain.Tests;

public class YearEndMathTests
{
    [Fact]
    public void Sums_only_the_requested_calendar_year()
    {
        var entries = new[]
        {
            Line(LedgerEntryKind.Charge, ChargeCategory.Rent, 2000m, new DateOnly(2025, 12, 1)),
            Line(LedgerEntryKind.Charge, ChargeCategory.Rent, 2000m, new DateOnly(2026, 1, 1)),
            Line(LedgerEntryKind.Charge, ChargeCategory.Nsf, 45m, new DateOnly(2026, 2, 3)),
            Line(LedgerEntryKind.Charge, ChargeCategory.Other, 80m, new DateOnly(2026, 3, 1)),
            Line(LedgerEntryKind.Payment, null, 2000m, new DateOnly(2026, 1, 2)),
            Line(LedgerEntryKind.Payment, null, 500m, new DateOnly(2027, 1, 2))
        };

        var totals = YearEndMath.Summarize(2026, entries);

        Assert.Equal(2000m, totals.RentCharged);
        Assert.Equal(45m, totals.NsfCharged);
        Assert.Equal(80m, totals.OtherCharged);
        Assert.Equal(2000m, totals.Payments);
    }

    private static LedgerEntry Line(LedgerEntryKind kind, ChargeCategory? category, decimal amount, DateOnly date) => new()
    {
        Kind = kind,
        ChargeCategory = category,
        PaymentMethod = kind == LedgerEntryKind.Payment ? PaymentMethod.ETransfer : null,
        Amount = amount,
        EffectiveDate = date
    };
}
