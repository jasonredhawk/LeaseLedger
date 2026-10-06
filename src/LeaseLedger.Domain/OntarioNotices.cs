namespace LeaseLedger.Domain;

public static class OntarioNotices
{
    public static readonly DateOnly N4SevenDayRuleStarts = new(2026, 9, 21);

    public static int N4NoticeDays(DateOnly noticeDate, RentFrequency frequency)
    {
        if (noticeDate >= N4SevenDayRuleStarts)
            return 7;

        return frequency is RentFrequency.Daily or RentFrequency.Weekly ? 7 : 14;
    }

    public static DateOnly EarliestIncreaseDate(
        DateOnly noticeDate,
        DateOnly tenancyStart,
        DateOnly? lastIncrease,
        RentFrequency frequency,
        int dueDay)
    {
        var notBefore = LaterOfTwelveMonthsOrNinetyDays(noticeDate, tenancyStart, lastIncrease);
        return frequency switch
        {
            RentFrequency.Daily => notBefore,
            RentFrequency.Weekly => SnapWeekly(notBefore, tenancyStart),
            RentFrequency.Yearly => SnapYearly(notBefore, tenancyStart),
            _ => SnapMonthly(notBefore, dueDay)
        };
    }

    public static string IncreaseExplanation(DateOnly noticeDate, DateOnly tenancyStart, DateOnly? lastIncrease, DateOnly earliest)
    {
        var twelveMonths = (lastIncrease ?? tenancyStart).AddMonths(12);
        var ninetyDays = noticeDate.AddDays(90);
        var notBefore = twelveMonths > ninetyDays ? twelveMonths : ninetyDays;
        var limiting = twelveMonths >= ninetyDays
            ? "12 months after the tenancy started or the last increase"
            : "90 days after the notice date";
        var snapped = earliest > notBefore ? " It then moves to the start of the next rent period." : "";
        return $"The earliest effective date is {earliest:yyyy-MM-dd}. That date is the later of {limiting}.{snapped}";
    }

    private static DateOnly LaterOfTwelveMonthsOrNinetyDays(DateOnly noticeDate, DateOnly tenancyStart, DateOnly? lastIncrease)
    {
        var twelveMonths = (lastIncrease ?? tenancyStart).AddMonths(12);
        var ninetyDays = noticeDate.AddDays(90);
        return twelveMonths > ninetyDays ? twelveMonths : ninetyDays;
    }

    public static DateOnly SnapMonthly(DateOnly earliest, int dueDay)
    {
        var clamped = ClampDay(earliest.Year, earliest.Month, dueDay);
        var candidate = new DateOnly(earliest.Year, earliest.Month, clamped);
        if (candidate >= earliest)
            return candidate;

        var next = earliest.AddMonths(1);
        return new DateOnly(next.Year, next.Month, ClampDay(next.Year, next.Month, dueDay));
    }

    public static DateOnly SnapWeekly(DateOnly earliest, DateOnly tenancyStart)
    {
        var days = (earliest.DayNumber - tenancyStart.DayNumber) % 7;
        if (days < 0)
            days += 7;

        return days == 0 ? earliest : earliest.AddDays(7 - days);
    }

    public static DateOnly SnapYearly(DateOnly earliest, DateOnly tenancyStart)
    {
        var candidate = new DateOnly(earliest.Year, tenancyStart.Month, ClampDay(earliest.Year, tenancyStart.Month, tenancyStart.Day));
        if (candidate >= earliest)
            return candidate;

        var year = earliest.Year + 1;
        return new DateOnly(year, tenancyStart.Month, ClampDay(year, tenancyStart.Month, tenancyStart.Day));
    }

    public static bool IsPeriodStart(DateOnly date, RentFrequency frequency, int dueDay, DateOnly tenancyStart)
    {
        return frequency switch
        {
            RentFrequency.Daily => true,
            RentFrequency.Weekly => (date.DayNumber - tenancyStart.DayNumber) % 7 == 0,
            RentFrequency.Yearly => date == SnapYearly(date, tenancyStart) && date.Month == tenancyStart.Month,
            _ => date.Day == ClampDay(date.Year, date.Month, dueDay)
        };
    }

    private static int ClampDay(int year, int month, int day)
    {
        var safe = day < 1 ? 1 : day;
        var max = DateTime.DaysInMonth(year, month);
        return safe > max ? max : safe;
    }
}

public sealed record N1Input(
    decimal CurrentRent,
    DateOnly TenancyStart,
    DateOnly? LastIncreaseDate,
    DateOnly NoticeDate,
    DateOnly ProposedEffectiveDate,
    RentFrequency Frequency,
    int RentDueDay,
    bool ExemptFromGuideline);

public sealed record N1Result(
    bool IsValid,
    IReadOnlyList<string> Errors,
    decimal? GuidelinePercent,
    decimal? NewRent,
    DateOnly? EarliestEffectiveDate,
    string Explanation);

public sealed record N4Input(
    DateOnly NoticeDate,
    RentFrequency Frequency,
    decimal Arrears);

public sealed record N4Result(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    decimal Arrears,
    int NoticeDays,
    DateOnly? TerminationDate);

public static class OntarioN1Calculator
{
    public static N1Result Calculate(N1Input input, IReadOnlyDictionary<int, decimal> guidelines)
    {
        if (input.ExemptFromGuideline)
        {
            return new N1Result(
                false,
                ["This unit is exempt from the rent increase guideline. N1 is the wrong form. Use the N2 helper, which does not apply a guideline cap."],
                null,
                null,
                null,
                "Guideline increases do not apply to this unit.");
        }

        var errors = new List<string>();
        if (input.CurrentRent <= 0)
            errors.Add("Current rent must be greater than zero.");

        if (input.RentDueDay is < 1 or > 31)
            errors.Add("Rent due day must be between 1 and 31.");

        var earliest = OntarioNotices.EarliestIncreaseDate(
            input.NoticeDate,
            input.TenancyStart,
            input.LastIncreaseDate,
            input.Frequency,
            input.RentDueDay);

        decimal? guideline = null;
        decimal? newRent = null;
        if (guidelines.TryGetValue(input.ProposedEffectiveDate.Year, out var percent))
        {
            guideline = percent;
            if (input.CurrentRent > 0)
                newRent = decimal.Round(input.CurrentRent * (1 + percent / 100m), 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            errors.Add($"No Ontario rent increase guideline is stored for {input.ProposedEffectiveDate.Year}.");
        }

        if (input.ProposedEffectiveDate < earliest)
            errors.Add($"The earliest valid effective date is {earliest:yyyy-MM-dd}. An increase needs 90 days' notice and 12 months since the tenancy started or the last increase, on the start of a rent period.");

        if (!OntarioNotices.IsPeriodStart(input.ProposedEffectiveDate, input.Frequency, input.RentDueDay, input.TenancyStart))
            errors.Add("The effective date must be the first day of a rental period.");

        var explanation = OntarioNotices.IncreaseExplanation(input.NoticeDate, input.TenancyStart, input.LastIncreaseDate, earliest);

        var valid = errors.Count == 0;
        return new N1Result(valid, errors, guideline, valid ? newRent : null, earliest, explanation);
    }
}

public sealed record N2Input(
    decimal CurrentRent,
    decimal ProposedRent,
    DateOnly TenancyStart,
    DateOnly? LastIncreaseDate,
    DateOnly NoticeDate,
    DateOnly ProposedEffectiveDate,
    RentFrequency Frequency,
    int RentDueDay,
    bool ExemptFromGuideline);

public sealed record N2Result(
    bool IsValid,
    IReadOnlyList<string> Errors,
    decimal? IncreaseAmount,
    decimal? IncreasePercent,
    DateOnly? EarliestEffectiveDate,
    string Explanation);

public static class OntarioN2Calculator
{
    public static N2Result Calculate(N2Input input)
    {
        if (!input.ExemptFromGuideline)
        {
            return new N2Result(
                false,
                ["This unit follows the rent increase guideline. N2 is the wrong form. Use the N1 helper."],
                null,
                null,
                null,
                "N2 is for units that are exempt from the guideline.");
        }

        var errors = new List<string>();
        if (input.CurrentRent <= 0)
            errors.Add("Current rent must be greater than zero.");

        var proposed = decimal.Round(input.ProposedRent, 2, MidpointRounding.AwayFromZero);
        if (proposed <= input.CurrentRent)
            errors.Add("The new rent must be higher than the current rent. N2 does not set a guideline cap, but it is still a rent increase.");

        if (input.RentDueDay is < 1 or > 31)
            errors.Add("Rent due day must be between 1 and 31.");

        var earliest = OntarioNotices.EarliestIncreaseDate(
            input.NoticeDate,
            input.TenancyStart,
            input.LastIncreaseDate,
            input.Frequency,
            input.RentDueDay);

        if (input.ProposedEffectiveDate < earliest)
            errors.Add($"The earliest valid effective date is {earliest:yyyy-MM-dd}. An increase needs 90 days' notice and 12 months since the tenancy started or the last increase, on the start of a rent period.");

        if (!OntarioNotices.IsPeriodStart(input.ProposedEffectiveDate, input.Frequency, input.RentDueDay, input.TenancyStart))
            errors.Add("The effective date must be the first day of a rental period.");

        var explanation = OntarioNotices.IncreaseExplanation(input.NoticeDate, input.TenancyStart, input.LastIncreaseDate, earliest)
            + " Because this unit is exempt from the guideline, the new rent is the amount you enter, not a percentage set by the province.";

        var valid = errors.Count == 0;
        decimal? increase = valid ? proposed - input.CurrentRent : null;
        decimal? percent = valid && input.CurrentRent > 0
            ? decimal.Round(increase!.Value / input.CurrentRent * 100m, 2, MidpointRounding.AwayFromZero)
            : null;

        return new N2Result(valid, errors, increase, percent, earliest, explanation);
    }
}

public static class OntarioN4Calculator
{
    public static N4Result Calculate(N4Input input)
    {
        var errors = new List<string>();
        if (input.Arrears <= 0)
            errors.Add("The ledger balance is not in arrears, so an N4 cannot be prepared.");

        var days = OntarioNotices.N4NoticeDays(input.NoticeDate, input.Frequency);
        var termination = input.NoticeDate.AddDays(days);
        IReadOnlyList<string> warnings = errors.Count == 0
            ? [NoticeCopy.DeemedServiceWarning, NoticeCopy.VoidIfPaidWarning]
            : [];

        return new N4Result(errors.Count == 0, errors, warnings, input.Arrears, days, errors.Count == 0 ? termination : null);
    }
}
