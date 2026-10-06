namespace LeaseLedger.Domain;

public sealed record YearEndTotals(int Year, decimal RentCharged, decimal OtherCharged, decimal NsfCharged, decimal Payments);

public static class YearEndMath
{
    public static YearEndTotals Summarize(int year, IEnumerable<LedgerEntry> entries)
    {
        decimal rent = 0, other = 0, nsf = 0, payments = 0;
        foreach (var entry in entries)
        {
            if (entry.EffectiveDate.Year != year)
                continue;

            if (entry.Kind == LedgerEntryKind.Payment)
            {
                payments += entry.Amount;
                continue;
            }

            switch (entry.ChargeCategory)
            {
                case ChargeCategory.Nsf:
                    nsf += entry.Amount;
                    break;
                case ChargeCategory.Other:
                    other += entry.Amount;
                    break;
                default:
                    rent += entry.Amount;
                    break;
            }
        }

        return new YearEndTotals(year, rent, other, nsf, payments);
    }
}
