namespace XKantor.LocalAgent.Core.Models;

// Migawka statusu identyfikacji Agenta - część AgentStatusReport, wypełniana przez Security
// (IdentityService/RenewalPolicy). Osobny, "płaski" model (nie referencja do Security), żeby
// Core nie musiało zależeć od Security.
public sealed record IdentityStatus
{
    public bool CzySparowany { get; init; }
    public string? StationId { get; init; }
    public bool CertyfikatWazny { get; init; }
    public bool WOkresieAwaryjnym { get; init; }
    public DateTimeOffset? WygasaUtc { get; init; }
    public int? DniDoWygasniecia { get; init; }
}

public sealed class AgentStatusReport
{
    public required string AgentVersion { get; init; }
    public required AgentOverallState OverallState { get; init; }
    public required IdentityStatus Identity { get; init; }
    public required IReadOnlyList<ModuleStatus> Modules { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
