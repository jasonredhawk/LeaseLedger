namespace LeaseLedger.Application;

public sealed class LeaseLedgerException : Exception
{
    public LeaseLedgerException(string message) : base(message)
    {
    }
}
