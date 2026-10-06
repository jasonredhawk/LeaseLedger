using LeaseLedger.Domain;

namespace LeaseLedger.Domain.Tests;

public class LedgerMathTests
{
    [Fact]
    public void Balance_is_charges_minus_payments()
    {
        var entries = new[]
        {
            Charge(2000m),
            Charge(2000m),
            Payment(2000m),
            Charge(45m, ChargeCategory.Nsf)
        };

        Assert.Equal(2045m, LedgerMath.Balance(entries));
    }

    [Fact]
    public void Balance_can_be_a_credit()
    {
        var entries = new[] { Charge(2000m), Payment(2500m) };

        Assert.Equal(-500m, LedgerMath.Balance(entries));
    }

    private static LedgerEntry Charge(decimal amount, ChargeCategory category = ChargeCategory.Rent) => new()
    {
        Kind = LedgerEntryKind.Charge,
        ChargeCategory = category,
        Amount = amount
    };

    private static LedgerEntry Payment(decimal amount) => new()
    {
        Kind = LedgerEntryKind.Payment,
        PaymentMethod = PaymentMethod.ETransfer,
        Amount = amount
    };
}
