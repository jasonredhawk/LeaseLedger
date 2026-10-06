using LeaseLedger.Domain;

namespace LeaseLedger.Domain.Tests;

public class OntarioN2CalculatorTests
{
    [Fact]
    public void Refuses_a_unit_that_follows_the_guideline()
    {
        var result = OntarioN2Calculator.Calculate(Sample(exempt: false, proposed: 2200m, effective: new DateOnly(2027, 6, 1)));

        Assert.False(result.IsValid);
        Assert.Contains("N2 is the wrong form", result.Errors[0]);
        Assert.Null(result.IncreaseAmount);
    }

    [Fact]
    public void Accepts_an_exempt_increase_on_a_valid_period_start()
    {
        var result = OntarioN2Calculator.Calculate(Sample(exempt: true, proposed: 2300m, effective: new DateOnly(2027, 6, 1)));

        Assert.True(result.IsValid);
        Assert.Equal(new DateOnly(2027, 1, 1), result.EarliestEffectiveDate);
        Assert.Equal(300m, result.IncreaseAmount);
        Assert.Equal(15m, result.IncreasePercent);
    }

    [Fact]
    public void Rejects_a_date_before_the_ninety_day_and_twelve_month_rules()
    {
        var result = OntarioN2Calculator.Calculate(Sample(exempt: true, proposed: 2300m, effective: new DateOnly(2026, 12, 1)));

        Assert.False(result.IsValid);
        Assert.Contains("2027-01-01", result.Errors[0]);
        Assert.Null(result.IncreaseAmount);
    }

    [Fact]
    public void Rejects_a_new_rent_that_is_not_an_increase()
    {
        var result = OntarioN2Calculator.Calculate(Sample(exempt: true, proposed: 2000m, effective: new DateOnly(2027, 6, 1)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("higher than the current rent", StringComparison.Ordinal));
    }

    private static N2Input Sample(bool exempt, decimal proposed, DateOnly effective) => new(
        CurrentRent: 2000m,
        ProposedRent: proposed,
        TenancyStart: new DateOnly(2024, 6, 1),
        LastIncreaseDate: new DateOnly(2025, 6, 1),
        NoticeDate: new DateOnly(2026, 9, 22),
        ProposedEffectiveDate: effective,
        Frequency: RentFrequency.Monthly,
        RentDueDay: 1,
        ExemptFromGuideline: exempt);
}
