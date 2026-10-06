namespace LeaseLedger.Domain;

public static class ProvincialNotices
{
    public static bool Supports(string? province) =>
        string.Equals(province, "ON", StringComparison.OrdinalIgnoreCase);

    public static string UnsupportedMessage(string? province) =>
        $"Notice helpers are loaded for Ontario only. This property is {province}. The ledger, maintenance, payments, and insurance still apply.";
}

public sealed record TerminationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    int NoticeDays,
    DateOnly? TerminationDate,
    string Explanation);

public static class OntarioN5Calculator
{
    public static TerminationResult Calculate(DateOnly noticeDate, bool secondNotice, DateOnly? firstNoticeDate)
    {
        var errors = new List<string>();
        if (secondNotice)
        {
            if (firstNoticeDate is null)
                errors.Add("Enter the date of the first N5. A second notice is only for the same conduct within 6 months.");
            else if (noticeDate < firstNoticeDate.Value.AddDays(7))
                errors.Add("A second N5 cannot be given until at least 7 days after the first N5.");
            else if (noticeDate > firstNoticeDate.Value.AddMonths(6))
                errors.Add("More than 6 months have passed since the first N5, so this is not a second notice.");
        }

        var days = secondNotice ? 14 : 20;
        var termination = errors.Count == 0 ? noticeDate.AddDays(days) : (DateOnly?)null;
        var warnings = errors.Count == 0
            ? new[]
            {
                secondNotice
                    ? "A second N5 in 6 months cannot be voided by correcting the problem."
                    : "The first N5 is void if the tenant corrects the problem within 7 days.",
                NoticeCopy.DeemedServiceWarning
            }
            : Array.Empty<string>();

        var explanation = secondNotice
            ? "A second N5 within 6 months uses 14 days, and only after 7 days have passed since the first notice."
            : "A first N5 uses 20 days, and the tenant has 7 days to correct the conduct, the damage, or the overcrowding.";

        return new TerminationResult(errors.Count == 0, errors, warnings, days, termination, explanation);
    }
}

public static class OntarioN6Calculator
{
    public static TerminationResult Calculate(DateOnly noticeDate, N6Reason reason, bool afterVoidableN5)
    {
        var days = reason == N6Reason.DrugOffence ? 10 : afterVoidableN5 ? 14 : 20;
        var termination = noticeDate.AddDays(days);
        var explanation = reason == N6Reason.DrugOffence
            ? "An N6 for producing or trafficking an illegal drug uses 10 days."
            : afterVoidableN5
                ? "After a voidable N5, this N6 reason uses 14 days when the conduct is inside the 6-month window."
                : "This N6 reason uses 20 days.";

        return new TerminationResult(
            true,
            [],
            [NoticeCopy.DeemedServiceWarning, "An N6 is not voided by the tenant stopping the conduct."],
            days,
            termination,
            explanation);
    }
}

public static class OntarioN7Calculator
{
    public static TerminationResult Calculate(DateOnly noticeDate)
    {
        var termination = noticeDate.AddDays(10);
        return new TerminationResult(
            true,
            [],
            [
                NoticeCopy.DeemedServiceWarning,
                "An N7 is not voidable. An L2 application has to be filed no later than 30 days after the termination date on the notice."
            ],
            10,
            termination,
            "An N7 for serious problems uses 10 days.");
    }
}

public sealed record N3Input(
    bool IsCareHome,
    bool IncreasingRent,
    decimal CurrentRent,
    decimal ProposedRent,
    decimal CurrentCareAndMeals,
    decimal ProposedCareAndMeals,
    DateOnly TenancyStart,
    DateOnly? LastIncreaseDate,
    DateOnly NoticeDate,
    DateOnly EffectiveDate,
    RentFrequency Frequency,
    int RentDueDay,
    bool ExemptFromGuideline);

public sealed record N3Result(
    bool IsValid,
    IReadOnlyList<string> Errors,
    decimal? NewRent,
    decimal? NewCareAndMeals,
    DateOnly? EarliestDate,
    string Explanation);

public static class OntarioN3Calculator
{
    public static N3Result Calculate(N3Input input, IReadOnlyDictionary<int, decimal> guidelines)
    {
        if (!input.IsCareHome)
        {
            return new N3Result(
                false,
                ["This unit is not marked as a care home. N3 is the wrong form. Use N1 or N2 for a rent increase."],
                null,
                null,
                null,
                "N3 is only for a care home rent increase or an increase in care services or meals.");
        }

        var errors = new List<string>();
        var rentChanging = input.IncreasingRent && input.ProposedRent != input.CurrentRent;
        var careChanging = input.ProposedCareAndMeals != input.CurrentCareAndMeals;
        if (!rentChanging && !careChanging)
            errors.Add("Increase the rent, the care and meal charge, or both.");

        DateOnly? earliest = null;
        decimal? newRent = null;
        if (rentChanging)
        {
            if (input.ProposedRent <= input.CurrentRent)
                errors.Add("The new rent must be higher than the current rent.");

            earliest = OntarioNotices.EarliestIncreaseDate(input.NoticeDate, input.TenancyStart, input.LastIncreaseDate, input.Frequency, input.RentDueDay);
            if (input.EffectiveDate < earliest)
                errors.Add($"The earliest date for a rent increase is {earliest:yyyy-MM-dd}.");
            if (!OntarioNotices.IsPeriodStart(input.EffectiveDate, input.Frequency, input.RentDueDay, input.TenancyStart))
                errors.Add("A rent increase must fall on the first day of a rental period.");

            if (!input.ExemptFromGuideline)
            {
                if (!guidelines.TryGetValue(input.EffectiveDate.Year, out var percent))
                    errors.Add($"No Ontario rent increase guideline is stored for {input.EffectiveDate.Year}.");
                else
                {
                    var cap = decimal.Round(input.CurrentRent * (1 + percent / 100m), 2, MidpointRounding.AwayFromZero);
                    if (input.ProposedRent > cap)
                        errors.Add($"The guideline for {input.EffectiveDate.Year} caps this rent at {cap:0.00} unless the Board orders more.");
                }
            }

            if (errors.Count == 0)
                newRent = decimal.Round(input.ProposedRent, 2, MidpointRounding.AwayFromZero);
        }

        decimal? newCare = null;
        var careEarliest = input.NoticeDate.AddDays(90);
        if (careChanging)
        {
            if (input.ProposedCareAndMeals <= input.CurrentCareAndMeals)
                errors.Add("The new care and meal charge must be higher than the current charge. There is no guideline cap on that charge.");
            if (input.EffectiveDate < careEarliest)
                errors.Add($"Care services and meals need 90 days' notice. The earliest date is {careEarliest:yyyy-MM-dd}.");
            if (errors.Count == 0)
                newCare = decimal.Round(input.ProposedCareAndMeals, 2, MidpointRounding.AwayFromZero);
        }

        if (rentChanging && careChanging && earliest is DateOnly rentEarliest && careEarliest > rentEarliest)
            earliest = careEarliest;

        var explanation = "N3 is for a care home. Rent still needs 90 days, 12 months, and a period start. Care services and meals need 90 days and have no guideline cap. Do not give an N3 before the tenant has the care home information package.";
        var valid = errors.Count == 0;
        return new N3Result(valid, errors, valid ? newRent : null, valid ? newCare : null, earliest ?? (careChanging ? careEarliest : null), explanation);
    }
}
