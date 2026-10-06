using LeaseLedger.Domain;

namespace LeaseLedger.Domain.Tests;

public class RentDueTests
{
    [Fact]
    public void Next_monthly_due_date_stays_in_the_month_when_today_is_on_or_before_it()
    {
        var next = RentDue.NextOnOrAfter(
            new DateOnly(2026, 9, 1),
            new DateOnly(2024, 6, 1),
            1,
            RentFrequency.Monthly);

        Assert.Equal(new DateOnly(2026, 9, 1), next);
    }

    [Fact]
    public void Next_monthly_due_date_rolls_to_the_following_month()
    {
        var next = RentDue.NextOnOrAfter(
            new DateOnly(2026, 9, 2),
            new DateOnly(2024, 6, 1),
            1,
            RentFrequency.Monthly);

        Assert.Equal(new DateOnly(2026, 10, 1), next);
    }
}
