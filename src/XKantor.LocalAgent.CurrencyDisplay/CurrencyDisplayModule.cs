using System.Runtime.Versioning;
using XKantor.LocalAgent.Core.Models;
using XKantor.LocalAgent.Core.Modules;

namespace XKantor.LocalAgent.CurrencyDisplay;

[SupportedOSPlatform("windows")]
public sealed class CurrencyDisplayModule : IAgentModule
{
    private readonly CurrencyDisplayService _service;

    public CurrencyDisplayModule(CurrencyDisplayService service)
    {
        _service = service;
    }

    public string Name => "CurrencyDisplay";

    public Task<ModuleStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var adapter = _service.ZbudujAdapter();
        return Task.FromResult(new ModuleStatus
        {
            Name = Name,
            State = adapter.IsConfigured ? ModuleState.Ready : ModuleState.NotConfigured,
            Details = adapter.IsConfigured ? $"Adapter: {adapter.Name}" : "Brak skonfigurowanego wyświetlacza."
        });
    }
}
