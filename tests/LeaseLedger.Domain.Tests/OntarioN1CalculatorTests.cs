using LeaseLedger.Domain;

namespace LeaseLedger.Domain.Tests;

public class OntarioN1CalculatorTests
{
    private static IReadOnlyDictionary<int, decimal> Guidelines { get; } =
        OntarioRentGuidelines.SeedCatalog.ToDictionary(row => row.Year, row => row.Percent);

    [Fact]
    public void Uses_2027_guideline_when_ninety_days_pushes_past_the_anniversary()
    {
        var result = OntarioN1Calculator.Calculate(new N1Input(
            CurrentRent: 2000m,
            TenancyStart: new DateOnly(2024, 6, 1),
            LastIncreaseDate: new DateOnly(2025, 6, 1),
            NoticeDate: new DateOnly(2026, 9, 21),
            ProposedEffectiveDate: new DateOnly(2027, 6, 1),
            Frequency: RentFrequency.Monthly,
            RentDueDay: 1,
            ExemptFromGuideline: false), Guidelines);

        Assert.True(result.IsValid);
        Assert.Equal(new DateOnly(2027, 1, 1), result.EarliestEffectiveDate);
        Assert.Equal(1.9m, result.GuidelinePercent);
        Assert.Equal(2038.00m, result.NewRent);
    }

    [Fact]
    public void Uses_2026_guideline_of_2_1_percent()
    {
        var result = OntarioN1Calculator.Calculate(new N1Input(
            CurrentRent: 1000m,
            TenancyStart: new DateOnly(2025, 1, 1),
            LastIncreaseDate: null,
            NoticeDate: new DateOnly(2026, 1, 1),
            ProposedEffectiveDate: new DateOnly(2026, 4, 1),
            Frequency: RentFrequency.Monthly,
            RentDueDay: 1,
            ExemptFromGuideline: false), Guidelines);

        Assert.True(result.IsValid);
        Assert.Equal(new DateOnly(2026, 4, 1), result.EarliestEffectiveDate);
        Assert.Equal(2.1m, result.GuidelinePercent);
        Assert.Equal(1021.00m, result.NewRent);
    }

    [Fact]
    public void Rejects_a_date_before_the_ninety_day_and_twelve_month_rules()
    {
        var result = OntarioN1Calculator.Calculate(new N1Input(
            CurrentRent: 2000m,
            TenancyStart: new DateOnly(2024, 6, 1),
            LastIncreaseDate: new DateOnly(2025, 6, 1),
            NoticeDate: new DateOnly(2026, 9, 21),
            ProposedEffectiveDate: new DateOnly(2026, 12, 1),
            Frequency: RentFrequency.Monthly,
            RentDueDay: 1,
            ExemptFromGuideline: false), Guidelines);

        Assert.False(result.IsValid);
        Assert.Contains("2027-01-01", result.Errors[0]);
        Assert.Null(result.NewRent);
    }

    [Fact]
    public void Twelve_month_rule_can_be_later_than_ninety_days()
    {
        var result = OntarioN1Calculator.Calculate(new N1Input(
            CurrentRent: 1500m,
            TenancyStart: new DateOnly(2024, 1, 1),
            LastIncreaseDate: new DateOnly(2026, 1, 1),
            NoticeDate: new DateOnly(2026, 3, 1),
            ProposedEffectiveDate: new DateOnly(2026, 6, 1),
            Frequency: RentFrequency.Monthly,
            RentDueDay: 1,
            ExemptFromGuideline: false), Guidelines);

        Assert.False(result.IsValid);
        Assert.Equal(new DateOnly(2027, 1, 1), result.EarliestEffectiveDate);
    }

    [Fact]
    public void Rejects_an_effective_date_that_is_not_a_rent_period_start()
    {
        var result = OntarioN1Calculator.Calculate(new N1Input(
            CurrentRent: 2000m,
            TenancyStart: new DateOnly(2024, 6, 1),
            LastIncreaseDate: new DateOnly(2025, 6, 1),
            NoticeDate: new DateOnly(2026, 9, 21),
            ProposedEffectiveDate: new DateOnly(2027, 1, 15),
            Frequency: RentFrequency.Monthly,
            RentDueDay: 1,
            ExemptFromGuideline: false), Guidelines);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("first day of a rental period", StringComparison.Ordinal));
    }

    [Fact]
    public void Stops_when_the_unit_is_exempt_from_the_guideline()
    {
        var result = OntarioN1Calculator.Calculate(new N1Input(
            CurrentRent: 2000m,
            TenancyStart: new DateOnly(2024, 6, 1),
            LastIncreaseDate: null,
            NoticeDate: new DateOnly(2026, 9, 21),
            ProposedEffectiveDate: new DateOnly(2027, 6, 1),
            Frequency: RentFrequency.Monthly,
            RentDueDay: 1,
            ExemptFromGuideline: true), Guidelines);

        Assert.False(result.IsValid);
        Assert.Contains("N1 is the wrong form", result.Errors[0]);
        Assert.Null(result.GuidelinePercent);
        Assert.Null(result.NewRent);
    }

    [Fact]
    public void Rounds_the_new_rent_to_cents()
    {
        var result = OntarioN1Calculator.Calculate(new N1Input(
            CurrentRent: 1000.05m,
            TenancyStart: new DateOnly(2025, 1, 1),
            LastIncreaseDate: null,
            NoticeDate: new DateOnly(2026, 1, 1),
            ProposedEffectiveDate: new DateOnly(2027, 1, 1),
            Frequency: RentFrequency.Monthly,
            RentDueDay: 1,
            ExemptFromGuideline: false), Guidelines);

        Assert.True(result.IsValid);
        Assert.Equal(1019.05m, result.NewRent);
    }

    [Fact]
    public void Weekly_tenancy_snaps_forward_to_the_next_period_start()
    {
        var result = OntarioN1Calculator.Calculate(new N1Input(
            CurrentRent: 400m,
            TenancyStart: new DateOnly(2026, 1, 5),
            LastIncreaseDate: null,
            NoticeDate: new DateOnly(2026, 6, 1),
            ProposedEffectiveDate: new DateOnly(2027, 1, 11),
            Frequency: RentFrequency.Weekly,
            RentDueDay: 1,
            ExemptFromGuideline: false), Guidelines);

        Assert.Equal(new DateOnly(2027, 1, 11), result.EarliestEffectiveDate);
        Assert.True(result.IsValid);
        Assert.Equal(1.9m, result.GuidelinePercent);
    }
}
