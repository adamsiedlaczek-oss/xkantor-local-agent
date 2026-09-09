namespace XKantor.LocalAgent.Core.Models;

// Status jednego modułu, agregowany przez AgentStatusService (patrz Modules/IAgentModule.cs).
public sealed class ModuleStatus
{
    public required string Name { get; init; }
    public required ModuleState State { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset CheckedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
