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
        var config = _service.GetConfig();
        var gotowy = config.Enabled && !string.IsNullOrWhiteSpace(config.MonitorStableId) && !string.IsNullOrWhiteSpace(config.BoardUrl);

        return Task.FromResult(new ModuleStatus
        {
            Name = Name,
            State = gotowy ? ModuleState.Ready : ModuleState.NotConfigured,
            Details = gotowy
                ? $"Tablica skonfigurowana: {config.MonitorLabel ?? config.MonitorStableId}."
                : "Tablica kursów nie jest jeszcze skonfigurowana na tym stanowisku."
        });
    }
}
