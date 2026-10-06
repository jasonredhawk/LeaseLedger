using LeaseLedger.Domain;

namespace LeaseLedger.Domain.Tests;

public class RentHistoryTests
{
    private static readonly DateOnly Today = new(2026, 10, 5);

    [Fact]
    public void Payment_on_the_due_date_is_on_time()
    {
        var rated = RentHistory.Recent(
            [
                Rent(new DateOnly(2026, 9, 1)),
                Pay(new DateOnly(2026, 9, 1))
            ],
            Today);

        var period = Assert.Single(rated);
        Assert.Equal(RentTimeliness.OnTime, period.Timeliness);
        Assert.Equal(new DateOnly(2026, 9, 1), period.PaidOn);
    }

    [Fact]
    public void Payment_after_the_due_date_is_late_and_an_open_charge_is_unpaid()
    {
        var rated = RentHistory.Recent(
            [
                Rent(new DateOnly(2026, 8, 1)),
                Pay(new DateOnly(2026, 8, 2)),
                Rent(new DateOnly(2026, 9, 1))
            ],
            Today);

        Assert.Equal(2, rated.Count);
        Assert.Equal(RentTimeliness.Late, rated[0].Timeliness);
        Assert.Equal(new DateOnly(2026, 8, 2), rated[0].PaidOn);
        Assert.Equal(RentTimeliness.Unpaid, rated[1].Timeliness);
        Assert.Null(rated[1].PaidOn);
    }

    [Fact]
    public void Early_payment_that_covers_later_rent_is_on_time()
    {
        var rated = RentHistory.Recent(
            [
                Pay(new DateOnly(2026, 7, 28), 4000m),
                Rent(new DateOnly(2026, 8, 1)),
                Rent(new DateOnly(2026, 9, 1))
            ],
            Today);

        Assert.All(rated, period => Assert.Equal(RentTimeliness.OnTime, period.Timeliness));
        Assert.Equal(new DateOnly(2026, 7, 28), rated[0].PaidOn);
        Assert.Equal(new DateOnly(2026, 7, 28), rated[1].PaidOn);
    }

    [Fact]
    public void Partial_payment_stays_unpaid_until_the_rent_is_cleared()
    {
        var stillShort = RentHistory.Recent(
            [Rent(new DateOnly(2026, 9, 1)), Pay(new DateOnly(2026, 9, 1), 500m)],
            Today);
        Assert.Equal(RentTimeliness.Unpaid, Assert.Single(stillShort).Timeliness);

        var clearedLate = RentHistory.Recent(
            [
                Rent(new DateOnly(2026, 9, 1)),
                Pay(new DateOnly(2026, 9, 1), 500m),
                Pay(new DateOnly(2026, 9, 12), 1500m)
            ],
            Today);
        var period = Assert.Single(clearedLate);
        Assert.Equal(RentTimeliness.Late, period.Timeliness);
        Assert.Equal(new DateOnly(2026, 9, 12), period.PaidOn);
    }

    [Fact]
    public void Nsf_and_other_charges_do_not_consume_the_rent_payment()
    {
        var rated = RentHistory.Recent(
            [
                Rent(new DateOnly(2026, 9, 1)),
                Charge(new DateOnly(2026, 9, 3), 45m, ChargeCategory.Nsf),
                Pay(new DateOnly(2026, 9, 1))
            ],
            Today);

        Assert.Equal(RentTimeliness.OnTime, Assert.Single(rated).Timeliness);
    }

    [Fact]
    public void Only_the_last_five_rents_that_have_come_due_are_scored()
    {
        var entries = new List<LedgerEntry>();
        for (var month = 1; month <= 6; month++)
        {
            var due = new DateOnly(2026, month, 1);
            entries.Add(Rent(due));
            entries.Add(Pay(due));
        }

        entries.Add(Rent(new DateOnly(2026, 11, 1)));

        var rated = RentHistory.Recent(entries, Today);

        Assert.Equal(5, rated.Count);
        Assert.Equal(new DateOnly(2026, 2, 1), rated[0].DueOn);
        Assert.Equal(new DateOnly(2026, 6, 1), rated[^1].DueOn);
        Assert.All(rated, period => Assert.Equal(RentTimeliness.OnTime, period.Timeliness));
    }

    [Fact]
    public void Future_rent_is_left_out_until_its_due_date()
    {
        var rated = RentHistory.Recent(
            [Rent(new DateOnly(2026, 11, 1))],
            Today);

        Assert.Empty(rated);
    }

    [Fact]
    public void Negative_amounts_are_rejected()
    {
        var entries = new[] { Rent(new DateOnly(2026, 9, 1), -1m) };

        Assert.Throws<ArgumentOutOfRangeException>(() => RentHistory.Recent(entries, Today));
    }

    private static LedgerEntry Rent(DateOnly due, decimal amount = 2000m) =>
        Charge(due, amount, ChargeCategory.Rent);

    private static LedgerEntry Charge(DateOnly due, decimal amount, ChargeCategory category) => new()
    {
        Id = Guid.NewGuid(),
        Kind = LedgerEntryKind.Charge,
        ChargeCategory = category,
        Amount = amount,
        EffectiveDate = due
    };

    private static LedgerEntry Pay(DateOnly paidOn, decimal amount = 2000m) => new()
    {
        Id = Guid.NewGuid(),
        Kind = LedgerEntryKind.Payment,
        PaymentMethod = PaymentMethod.ETransfer,
        Amount = amount,
        EffectiveDate = paidOn
    };
}
