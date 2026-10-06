namespace LeaseLedger.Domain;

public static class RentDue
{
    public static DateOnly NextOnOrAfter(DateOnly today, DateOnly tenancyStart, int dueDay, RentFrequency frequency)
    {
        var from = today > tenancyStart ? today : tenancyStart;
        return frequency switch
        {
            RentFrequency.Daily => from,
            RentFrequency.Weekly => OntarioNotices.SnapWeekly(from, tenancyStart),
            RentFrequency.Yearly => OntarioNotices.SnapYearly(from, tenancyStart),
            _ => OntarioNotices.SnapMonthly(from, dueDay)
        };
    }
}
