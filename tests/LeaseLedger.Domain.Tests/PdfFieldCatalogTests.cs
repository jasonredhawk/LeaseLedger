using LeaseLedger.Domain;

namespace LeaseLedger.Domain.Tests;

public class PdfFieldCatalogTests
{
    [Theory]
    [InlineData("TenantName", PdfFieldCatalog.TenantName)]
    [InlineData("form1[0].Page1[0].TenantName[0]", PdfFieldCatalog.TenantName)]
    [InlineData("tenant_email", PdfFieldCatalog.TenantEmail)]
    [InlineData("Current Rent", PdfFieldCatalog.RentAmount)]
    [InlineData("postal-code", PdfFieldCatalog.PostalCode)]
    public void Suggests_an_obvious_field_name(string fieldName, string expected) =>
        Assert.Equal(expected, PdfFieldCatalog.Suggest(fieldName));

    [Theory]
    [InlineData("Email")]
    [InlineData("Name")]
    [InlineData("Comments")]
    public void Leaves_ambiguous_names_unmapped(string fieldName) =>
        Assert.Null(PdfFieldCatalog.Suggest(fieldName));

    [Fact]
    public void Formats_dates_and_money()
    {
        var context = Sample();

        Assert.Equal("2024-03-01", PdfFieldCatalog.Resolve(context, PdfFieldCatalog.StartDate, null, PdfValueFormat.Default));
        Assert.Equal("01/03/2024", PdfFieldCatalog.Resolve(context, PdfFieldCatalog.StartDate, null, PdfValueFormat.DateDayFirst));
        Assert.Equal("03/01/2024", PdfFieldCatalog.Resolve(context, PdfFieldCatalog.StartDate, null, PdfValueFormat.DateMonthFirst));
        Assert.Equal("2024/03/01", PdfFieldCatalog.Resolve(context, PdfFieldCatalog.StartDate, null, PdfValueFormat.DateYearSlash));
        Assert.Equal("", PdfFieldCatalog.Resolve(context, PdfFieldCatalog.LastIncreaseDate, null, PdfValueFormat.DateIso));
        Assert.Equal("$1,850.00", PdfFieldCatalog.Resolve(context, PdfFieldCatalog.RentAmount, null, PdfValueFormat.Money));
        Assert.Equal("1850.00", PdfFieldCatalog.Resolve(context, PdfFieldCatalog.RentAmount, null, PdfValueFormat.PlainDecimal));
        Assert.Equal("-$40.50", PdfFieldCatalog.Resolve(context, PdfFieldCatalog.Balance, null, PdfValueFormat.Default));
    }

    [Fact]
    public void Constant_text_overrides_a_data_token()
    {
        var value = PdfFieldCatalog.Resolve(Sample(), PdfFieldCatalog.TenantName, "  12 Main St  ", PdfValueFormat.Default);
        Assert.Equal("12 Main St", value);
    }

    [Fact]
    public void Builds_a_one_line_address()
    {
        var address = PdfFieldCatalog.Resolve(Sample(), PdfFieldCatalog.Address, null, PdfValueFormat.Default);
        Assert.Equal("10 King St, unit 2B, Toronto ON M5V 2T6", address);
    }

    private static PdfFillContext Sample() => new(
        "Ada Lovelace",
        "ada@example.com",
        1850m,
        1,
        RentFrequency.Monthly,
        new DateOnly(2024, 3, 1),
        null,
        "2B",
        "10 King St",
        "Toronto",
        "ON",
        "M5V 2T6",
        "Grace Hopper",
        "grace@example.com",
        -40.50m,
        new DateOnly(2026, 10, 5));
}
