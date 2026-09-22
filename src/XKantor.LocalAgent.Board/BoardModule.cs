using XKantor.LocalAgent.Core.Models;
using XKantor.LocalAgent.Core.Modules;

namespace XKantor.LocalAgent.Board;

public sealed class BoardModule : IAgentModule
{
    private readonly BoardService _service;

    public BoardModule(BoardService service)
    {
        _service = service;
    }

    public string Name => "Board";

    public Task<ModuleStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var configi = _service.GetConfigs()
            .Where(c => c.Enabled && !string.IsNullOrWhiteSpace(c.MonitorStableId) && !string.IsNullOrWhiteSpace(c.BoardUrl))
            .ToList();

        return Task.FromResult(new ModuleStatus
        {
            Name = Name,
            State = configi.Count > 0 ? ModuleState.Ready : ModuleState.NotConfigured,
            Details = configi.Count > 0
                ? $"Tablica skonfigurowana na {configi.Count} monitor(ach): {string.Join(", ", configi.Select(c => c.MonitorLabel ?? c.MonitorStableId))}."
                : "Tablica kursów nie jest jeszcze skonfigurowana na tym stanowisku."
        });
    }
}
