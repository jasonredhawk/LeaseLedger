using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LeaseLedger.Domain;

public static class PdfFormLimits
{
    public const int MaxBytes = 8 * 1024 * 1024;
}

public sealed record PdfDataToken(string Key, string Label, PdfTokenKind Kind);

public sealed record PdfFillContext(
    string TenantName,
    string TenantEmail,
    decimal RentAmount,
    int RentDueDay,
    RentFrequency Frequency,
    DateOnly StartDate,
    DateOnly? LastRentIncreaseDate,
    string UnitLabel,
    string Street,
    string City,
    string Province,
    string PostalCode,
    string LandlordName,
    string LandlordEmail,
    decimal Balance,
    DateOnly Today);

public static partial class PdfFieldCatalog
{
    public const string TenantName = "tenant.name";
    public const string TenantEmail = "tenant.email";
    public const string RentAmount = "rent.amount";
    public const string RentDueDay = "rent.dueDay";
    public const string FrequencyKey = "rent.frequency";
    public const string StartDate = "tenancy.startDate";
    public const string LastIncreaseDate = "tenancy.lastIncreaseDate";
    public const string UnitLabel = "unit.label";
    public const string Street = "property.street";
    public const string City = "property.city";
    public const string Province = "property.province";
    public const string PostalCode = "property.postalCode";
    public const string Address = "property.address";
    public const string LandlordName = "landlord.name";
    public const string LandlordEmail = "landlord.email";
    public const string Balance = "ledger.balance";
    public const string Today = "today";

    private static readonly CultureInfo Canada = CultureInfo.GetCultureInfo("en-CA");

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["tenantname"] = TenantName,
        ["tenantsname"] = TenantName,
        ["nameoftenant"] = TenantName,
        ["rentername"] = TenantName,
        ["residentname"] = TenantName,
        ["tenantemail"] = TenantEmail,
        ["tenantsemail"] = TenantEmail,
        ["renteremail"] = TenantEmail,
        ["rentamount"] = RentAmount,
        ["currentrent"] = RentAmount,
        ["monthlyrent"] = RentAmount,
        ["baserent"] = RentAmount,
        ["rent"] = RentAmount,
        ["rentdueday"] = RentDueDay,
        ["dueday"] = RentDueDay,
        ["rentfrequency"] = FrequencyKey,
        ["paymentfrequency"] = FrequencyKey,
        ["frequency"] = FrequencyKey,
        ["startdate"] = StartDate,
        ["tenancystartdate"] = StartDate,
        ["leasestartdate"] = StartDate,
        ["commencementdate"] = StartDate,
        ["lastrentincreasedate"] = LastIncreaseDate,
        ["lastincreasedate"] = LastIncreaseDate,
        ["lastincrease"] = LastIncreaseDate,
        ["unitlabel"] = UnitLabel,
        ["unitnumber"] = UnitLabel,
        ["unitno"] = UnitLabel,
        ["unit"] = UnitLabel,
        ["apartment"] = UnitLabel,
        ["apt"] = UnitLabel,
        ["street"] = Street,
        ["streetaddress"] = Street,
        ["propertystreet"] = Street,
        ["city"] = City,
        ["propertycity"] = City,
        ["municipality"] = City,
        ["province"] = Province,
        ["propertyprovince"] = Province,
        ["postalcode"] = PostalCode,
        ["postal"] = PostalCode,
        ["zipcode"] = PostalCode,
        ["zip"] = PostalCode,
        ["propertyaddress"] = Address,
        ["fulladdress"] = Address,
        ["rentaladdress"] = Address,
        ["unitaddress"] = Address,
        ["address"] = Address,
        ["landlordname"] = LandlordName,
        ["landlord"] = LandlordName,
        ["lessorname"] = LandlordName,
        ["lessor"] = LandlordName,
        ["landlordemail"] = LandlordEmail,
        ["lessoremail"] = LandlordEmail,
        ["balance"] = Balance,
        ["currentbalance"] = Balance,
        ["amountowing"] = Balance,
        ["arrears"] = Balance,
        ["amountdue"] = Balance,
        ["today"] = Today,
        ["todaydate"] = Today,
        ["currentdate"] = Today,
        ["date"] = Today
    };

    public static IReadOnlyList<PdfDataToken> Tokens { get; } =
    [
        new(TenantName, "Tenant name", PdfTokenKind.Text),
        new(TenantEmail, "Tenant email", PdfTokenKind.Text),
        new(RentAmount, "Rent amount", PdfTokenKind.Money),
        new(RentDueDay, "Rent due day", PdfTokenKind.Text),
        new(FrequencyKey, "Rent frequency", PdfTokenKind.Text),
        new(StartDate, "Tenancy start date", PdfTokenKind.Date),
        new(LastIncreaseDate, "Last rent increase date", PdfTokenKind.Date),
        new(UnitLabel, "Unit label", PdfTokenKind.Text),
        new(Street, "Street", PdfTokenKind.Text),
        new(City, "City", PdfTokenKind.Text),
        new(Province, "Province", PdfTokenKind.Text),
        new(PostalCode, "Postal code", PdfTokenKind.Text),
        new(Address, "Full address", PdfTokenKind.Text),
        new(LandlordName, "Landlord name", PdfTokenKind.Text),
        new(LandlordEmail, "Landlord email", PdfTokenKind.Text),
        new(Balance, "Current balance", PdfTokenKind.Money),
        new(Today, "Today's date", PdfTokenKind.Date)
    ];

    public static PdfDataToken? Find(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : Tokens.FirstOrDefault(token => token.Key == key);

    public static string ShortLabel(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return "";

        var leaf = fieldName;
        var dot = fieldName.LastIndexOf('.');
        if (dot >= 0 && dot < fieldName.Length - 1)
            leaf = fieldName[(dot + 1)..];

        leaf = BracketPattern().Replace(leaf, "");
        if (leaf.StartsWith('#'))
            leaf = leaf[1..];

        return leaf.Length == 0 ? fieldName : leaf;
    }

    public static string? Suggest(string fieldName)
    {
        var normalized = Normalize(ShortLabel(fieldName));
        return normalized.Length == 0 ? null : Aliases.GetValueOrDefault(normalized);
    }

    public static string Resolve(PdfFillContext context, string? dataKey, string? constantValue, PdfValueFormat format)
    {
        if (!string.IsNullOrWhiteSpace(constantValue))
            return constantValue.Trim();

        var token = Find(dataKey);
        if (token is null)
            return "";

        return token.Kind switch
        {
            PdfTokenKind.Date => FormatDate(DateOf(context, token.Key), format),
            PdfTokenKind.Money => FormatMoney(MoneyOf(context, token.Key), format),
            _ => TextOf(context, token.Key)
        };
    }

    public static string OneLineAddress(string street, string unit, string city, string province, string postal)
    {
        var place = string.Join(" ", new[] { city, province, postal }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var line = string.IsNullOrWhiteSpace(unit) ? street.Trim() : $"{street.Trim()}, unit {unit.Trim()}";
        if (string.IsNullOrWhiteSpace(line))
            return place;
        return string.IsNullOrWhiteSpace(place) ? line : $"{line}, {place}";
    }

    private static string Normalize(string label)
    {
        var letters = new StringBuilder(label.Length);
        foreach (var character in label)
        {
            if (char.IsLetter(character))
                letters.Append(char.ToLowerInvariant(character));
        }

        return letters.ToString();
    }

    private static DateOnly? DateOf(PdfFillContext context, string key) => key switch
    {
        StartDate => context.StartDate,
        LastIncreaseDate => context.LastRentIncreaseDate,
        Today => context.Today,
        _ => null
    };

    private static decimal MoneyOf(PdfFillContext context, string key) => key switch
    {
        Balance => context.Balance,
        _ => context.RentAmount
    };

    private static string TextOf(PdfFillContext context, string key) => key switch
    {
        TenantName => context.TenantName,
        TenantEmail => context.TenantEmail,
        RentDueDay => context.RentDueDay.ToString(CultureInfo.InvariantCulture),
        FrequencyKey => FrequencyLabel(context.Frequency),
        UnitLabel => context.UnitLabel,
        Street => context.Street,
        City => context.City,
        Province => context.Province,
        PostalCode => context.PostalCode,
        Address => OneLineAddress(context.Street, context.UnitLabel, context.City, context.Province, context.PostalCode),
        LandlordName => context.LandlordName,
        LandlordEmail => context.LandlordEmail,
        _ => ""
    };

    private static string FormatDate(DateOnly? date, PdfValueFormat format)
    {
        if (date is null)
            return "";

        var pattern = format switch
        {
            PdfValueFormat.DateDayFirst => "dd/MM/yyyy",
            PdfValueFormat.DateMonthFirst => "MM/dd/yyyy",
            PdfValueFormat.DateYearSlash => "yyyy/MM/dd",
            _ => "yyyy-MM-dd"
        };
        return date.Value.ToString(pattern, CultureInfo.InvariantCulture);
    }

    private static string FormatMoney(decimal amount, PdfValueFormat format)
    {
        if (format == PdfValueFormat.PlainDecimal)
            return amount.ToString("0.00", CultureInfo.InvariantCulture);

        return amount.ToString("C", Canada);
    }

    private static string FrequencyLabel(RentFrequency frequency) => frequency switch
    {
        RentFrequency.Daily => "Daily",
        RentFrequency.Weekly => "Weekly",
        RentFrequency.Yearly => "Yearly",
        _ => "Monthly"
    };

    [GeneratedRegex(@"\[\d+\]")]
    private static partial Regex BracketPattern();
}
