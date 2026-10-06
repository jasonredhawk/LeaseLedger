using System.Globalization;
using LeaseLedger.Application;
using LeaseLedger.Domain;

namespace LeaseLedger.Shared;

public static class TextFormat
{
    private static readonly CultureInfo Canada = CultureInfo.GetCultureInfo("en-CA");

    public static string Money(decimal amount) => amount.ToString("C", Canada);

    public static string Date(DateOnly date) => date.ToString("MMMM d, yyyy", Canada);

    public static string When(DateTimeOffset value) => value.ToLocalTime().ToString("MMM d, yyyy h:mm tt", Canada);

    public static string Balance(decimal balance)
    {
        if (balance > 0)
            return $"{Money(balance)} owing";
        if (balance < 0)
            return $"{Money(-balance)} credit";
        return "Paid up";
    }

    public static string Method(PaymentMethod method) => method switch
    {
        PaymentMethod.ETransfer => "E-transfer",
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Cheque => "Cheque",
        _ => "Other"
    };

    public static string Frequency(RentFrequency frequency) => frequency switch
    {
        RentFrequency.Daily => "Daily",
        RentFrequency.Weekly => "Weekly",
        RentFrequency.Yearly => "Yearly",
        _ => "Monthly"
    };

    public static string RentRating(IReadOnlyList<RentPeriodDto> periods)
    {
        if (periods.Count == 0)
            return "No rent due yet";

        var onTime = periods.Count(period => period.Timeliness == RentTimeliness.OnTime);
        return periods.Count == 1
            ? $"Last rent {(onTime == 1 ? "on time" : periods[0].Timeliness == RentTimeliness.Late ? "late" : "unpaid")}"
            : $"{onTime} of {periods.Count} on time";
    }

    public static string RentPeriodChip(RentPeriodDto period)
    {
        var due = period.DueOn.ToString("MMM d", Canada);
        var status = period.Timeliness switch
        {
            RentTimeliness.OnTime => "on time",
            RentTimeliness.Late => "late",
            _ => "unpaid"
        };
        return $"{due} · {status}";
    }

    public static string RentPeriodDetail(RentPeriodDto period)
    {
        var due = Date(period.DueOn);
        return period.Timeliness switch
        {
            RentTimeliness.OnTime when period.PaidOn is DateOnly paid && paid < period.DueOn => $"{due}, paid early on {Date(paid)}",
            RentTimeliness.OnTime => $"{due}, paid on time",
            RentTimeliness.Late when period.PaidOn is DateOnly paid => $"{due}, paid late on {Date(paid)}",
            _ => $"{due}, still unpaid"
        };
    }
}
