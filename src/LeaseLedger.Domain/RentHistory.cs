namespace LeaseLedger.Domain;

public enum RentTimeliness
{
    OnTime,
    Late,
    Unpaid
}

public sealed record RentPeriodRating(DateOnly DueOn, decimal Amount, RentTimeliness Timeliness, DateOnly? PaidOn);

public static class RentHistory
{
    public const int RecentCount = 5;

    /// <summary>
    /// Scores the most recent rent charges that have come due.
    /// Payments are applied to rent in due-date order. A period is on time when the payment that clears it is on or before the due date.
    /// Other charges stay on the balance and do not change this rating.
    /// </summary>
    public static IReadOnlyList<RentPeriodRating> Recent(IEnumerable<LedgerEntry> entries, DateOnly asOf, int count = RecentCount)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        var all = entries.ToList();
        if (all.Any(entry => entry.Amount < 0))
            throw new ArgumentOutOfRangeException(nameof(entries), "Ledger amounts are stored as positive numbers.");

        var rents = all
            .Where(entry => entry.Kind == LedgerEntryKind.Charge
                && entry.ChargeCategory is null or ChargeCategory.Rent
                && entry.Amount > 0)
            .OrderBy(entry => entry.EffectiveDate)
            .ThenBy(entry => entry.Id)
            .ToList();

        var payments = all
            .Where(entry => entry.Kind == LedgerEntryKind.Payment && entry.Amount > 0)
            .OrderBy(entry => entry.EffectiveDate)
            .ThenBy(entry => entry.Id)
            .ToList();

        var paymentIndex = 0;
        var remaining = 0m;
        var rated = new List<RentPeriodRating>();

        foreach (var rent in rents)
        {
            var covered = 0m;
            DateOnly? paidOn = null;
            while (covered < rent.Amount && paymentIndex < payments.Count)
            {
                if (remaining == 0)
                    remaining = payments[paymentIndex].Amount;

                var applied = Math.Min(rent.Amount - covered, remaining);
                covered += applied;
                remaining -= applied;
                if (covered >= rent.Amount)
                    paidOn = payments[paymentIndex].EffectiveDate;

                if (remaining == 0)
                    paymentIndex++;
            }

            if (rent.EffectiveDate > asOf)
                continue;

            var timeliness = paidOn is null
                ? RentTimeliness.Unpaid
                : paidOn.Value <= rent.EffectiveDate ? RentTimeliness.OnTime : RentTimeliness.Late;

            rated.Add(new RentPeriodRating(
                rent.EffectiveDate,
                rent.Amount,
                timeliness,
                timeliness == RentTimeliness.Unpaid ? null : paidOn));
        }

        return rated.Count <= count ? rated : rated[^count..];
    }
}
