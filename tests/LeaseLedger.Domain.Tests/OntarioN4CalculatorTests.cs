using LeaseLedger.Domain;

namespace LeaseLedger.Domain.Tests;

public class OntarioN4CalculatorTests
{
    [Fact]
    public void Monthly_notice_on_the_rule_change_uses_seven_days()
    {
        var result = OntarioN4Calculator.Calculate(new N4Input(
            new DateOnly(2026, 9, 21),
            RentFrequency.Monthly,
            2000m));

        Assert.True(result.IsValid);
        Assert.Equal(7, result.NoticeDays);
        Assert.Equal(new DateOnly(2026, 9, 28), result.TerminationDate);
        Assert.Contains(result.Warnings, warning => warning.Contains("void", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Monthly_notice_before_the_rule_change_uses_fourteen_days()
    {
        var result = OntarioN4Calculator.Calculate(new N4Input(
            new DateOnly(2026, 9, 20),
            RentFrequency.Monthly,
            2000m));

        Assert.True(result.IsValid);
        Assert.Equal(14, result.NoticeDays);
        Assert.Equal(new DateOnly(2026, 10, 4), result.TerminationDate);
    }

    [Fact]
    public void Yearly_notice_before_the_rule_change_uses_fourteen_days()
    {
        var result = OntarioN4Calculator.Calculate(new N4Input(
            new DateOnly(2026, 1, 1),
            RentFrequency.Yearly,
            100m));

        Assert.Equal(14, result.NoticeDays);
        Assert.Equal(new DateOnly(2026, 1, 15), result.TerminationDate);
    }

    [Theory]
    [InlineData(RentFrequency.Daily)]
    [InlineData(RentFrequency.Weekly)]
    public void Short_periods_were_already_seven_days_before_the_change(RentFrequency frequency)
    {
        var result = OntarioN4Calculator.Calculate(new N4Input(
            new DateOnly(2026, 9, 20),
            frequency,
            80m));

        Assert.Equal(7, result.NoticeDays);
        Assert.Equal(new DateOnly(2026, 9, 27), result.TerminationDate);
    }

    [Fact]
    public void Rejects_a_ledger_that_is_not_in_arrears()
    {
        var result = OntarioN4Calculator.Calculate(new N4Input(
            new DateOnly(2026, 9, 21),
            RentFrequency.Monthly,
            0m));

        Assert.False(result.IsValid);
        Assert.Null(result.TerminationDate);
        Assert.Empty(result.Warnings);
    }
}
