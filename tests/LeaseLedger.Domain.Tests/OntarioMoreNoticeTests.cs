using LeaseLedger.Domain;

namespace LeaseLedger.Domain.Tests;

public class OntarioMoreNoticeTests
{
    [Fact]
    public void First_n5_uses_twenty_days_and_can_be_voided()
    {
        var result = OntarioN5Calculator.Calculate(new DateOnly(2026, 9, 22), secondNotice: false, firstNoticeDate: null);

        Assert.True(result.IsValid);
        Assert.Equal(20, result.NoticeDays);
        Assert.Equal(new DateOnly(2026, 10, 12), result.TerminationDate);
        Assert.Contains(result.Warnings, warning => warning.Contains("7 days", StringComparison.Ordinal));
    }

    [Fact]
    public void Second_n5_uses_fourteen_days_after_the_waiting_period()
    {
        var result = OntarioN5Calculator.Calculate(new DateOnly(2026, 9, 10), secondNotice: true, firstNoticeDate: new DateOnly(2026, 9, 1));

        Assert.True(result.IsValid);
        Assert.Equal(14, result.NoticeDays);
        Assert.Equal(new DateOnly(2026, 9, 24), result.TerminationDate);
    }

    [Fact]
    public void Second_n5_is_rejected_before_seven_days()
    {
        var result = OntarioN5Calculator.Calculate(new DateOnly(2026, 9, 5), secondNotice: true, firstNoticeDate: new DateOnly(2026, 9, 1));

        Assert.False(result.IsValid);
        Assert.Null(result.TerminationDate);
    }

    [Fact]
    public void N6_drug_reason_uses_ten_days()
    {
        var result = OntarioN6Calculator.Calculate(new DateOnly(2026, 9, 22), N6Reason.DrugOffence, afterVoidableN5: false);

        Assert.Equal(10, result.NoticeDays);
        Assert.Equal(new DateOnly(2026, 10, 2), result.TerminationDate);
    }

    [Fact]
    public void N6_other_reason_uses_twenty_days_unless_it_follows_an_n5()
    {
        var first = OntarioN6Calculator.Calculate(new DateOnly(2026, 9, 22), N6Reason.OtherIllegalAct, afterVoidableN5: false);
        var after = OntarioN6Calculator.Calculate(new DateOnly(2026, 9, 22), N6Reason.IncomeMisrepresentation, afterVoidableN5: true);

        Assert.Equal(20, first.NoticeDays);
        Assert.Equal(14, after.NoticeDays);
    }

    [Fact]
    public void N7_uses_ten_days_and_warns_about_the_filing_deadline()
    {
        var result = OntarioN7Calculator.Calculate(new DateOnly(2026, 9, 22));

        Assert.Equal(new DateOnly(2026, 10, 2), result.TerminationDate);
        Assert.Contains(result.Warnings, warning => warning.Contains("30 days", StringComparison.Ordinal));
    }

    [Fact]
    public void N3_refuses_a_unit_that_is_not_a_care_home()
    {
        var result = OntarioN3Calculator.Calculate(Sample(careHome: false), Guidelines);

        Assert.False(result.IsValid);
        Assert.Contains("N3 is the wrong form", result.Errors[0]);
    }

    [Fact]
    public void N3_care_charge_needs_ninety_days_and_has_no_cap()
    {
        var tooSoon = OntarioN3Calculator.Calculate(Sample(careHome: true, care: 900m, effective: new DateOnly(2026, 12, 20)), Guidelines);
        var ok = OntarioN3Calculator.Calculate(Sample(careHome: true, care: 900m, effective: new DateOnly(2026, 12, 21)), Guidelines);

        Assert.False(tooSoon.IsValid);
        Assert.True(ok.IsValid);
        Assert.Equal(900m, ok.NewCareAndMeals);
        Assert.Null(ok.NewRent);
    }

    private static IReadOnlyDictionary<int, decimal> Guidelines { get; } =
        OntarioRentGuidelines.SeedCatalog.ToDictionary(row => row.Year, row => row.Percent);

    private static N3Input Sample(bool careHome, decimal care = 800m, DateOnly? effective = null) => new(
        careHome,
        IncreasingRent: false,
        CurrentRent: 2000m,
        ProposedRent: 2000m,
        CurrentCareAndMeals: 800m,
        ProposedCareAndMeals: care,
        TenancyStart: new DateOnly(2024, 6, 1),
        LastIncreaseDate: new DateOnly(2025, 6, 1),
        NoticeDate: new DateOnly(2026, 9, 22),
        EffectiveDate: effective ?? new DateOnly(2026, 12, 21),
        Frequency: RentFrequency.Monthly,
        RentDueDay: 1,
        ExemptFromGuideline: false);
}
