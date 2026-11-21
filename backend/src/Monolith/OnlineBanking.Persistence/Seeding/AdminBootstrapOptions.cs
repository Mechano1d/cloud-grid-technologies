namespace OnlineBanking.Persistence.Seeding;

public sealed class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";

    public string? Email { get; init; }
    public string? Password { get; init; }
    public string? FullName { get; init; }
    public string? Phone { get; init; }
    public string? TaxId { get; init; }
    public DateOnly? BirthDate { get; init; }
}
