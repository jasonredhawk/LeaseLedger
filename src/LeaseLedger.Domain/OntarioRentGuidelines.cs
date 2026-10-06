namespace LeaseLedger.Domain;

public static class OntarioRentGuidelines
{
    public static IReadOnlyList<RentGuideline> SeedCatalog { get; } =
    [
        new() { Year = 2023, Percent = 2.5m },
        new() { Year = 2024, Percent = 2.5m },
        new() { Year = 2025, Percent = 2.5m },
        new() { Year = 2026, Percent = 2.1m },
        new() { Year = 2027, Percent = 1.9m }
    ];
}
