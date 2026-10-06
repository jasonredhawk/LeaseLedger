namespace LeaseLedger.Domain;

public static class LedgerMath
{
    public static decimal Balance(IEnumerable<LedgerEntry> entries)
    {
        decimal balance = 0;
        foreach (var entry in entries)
        {
            if (entry.Amount < 0)
                throw new ArgumentOutOfRangeException(nameof(entries), "Ledger amounts are stored as positive numbers.");

            balance += entry.Kind == LedgerEntryKind.Charge ? entry.Amount : -entry.Amount;
        }

        return balance;
    }
}
