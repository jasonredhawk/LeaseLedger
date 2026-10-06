namespace LeaseLedger.Domain;

public enum RentFrequency
{
    Daily,
    Weekly,
    Monthly,
    Yearly
}

public enum LedgerEntryKind
{
    Charge,
    Payment
}

public enum ChargeCategory
{
    Rent,
    Other,
    Nsf
}

public enum PaymentMethod
{
    ETransfer,
    Cash,
    Cheque,
    Card,
    Other
}

public enum MaintenanceStatus
{
    Submitted,
    Acknowledged,
    Scheduled,
    Completed
}

public enum NoticeKind
{
    N1,
    N2,
    N3,
    N4,
    N5,
    N6,
    N7
}

public enum N5Reason
{
    Interference,
    Damage,
    Overcrowding
}

public enum N6Reason
{
    DrugOffence,
    OtherIllegalAct,
    IncomeMisrepresentation
}

public enum ServiceMethod
{
    InPerson,
    Mail,
    Courier,
    Email
}

public enum InsuranceStatus
{
    Requested,
    Active,
    Cancelled
}

public enum PdfFieldKind
{
    Text,
    Checkbox,
    Radio,
    Choice,
    Signature
}

public enum PdfValueFormat
{
    Default,
    DateIso,
    DateDayFirst,
    DateMonthFirst,
    DateYearSlash,
    Money,
    PlainDecimal
}

public enum PdfTokenKind
{
    Text,
    Date,
    Money
}
