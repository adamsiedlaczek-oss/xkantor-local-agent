using System.Runtime.Versioning;
using XKantor.LocalAgent.Core.Models;
using XKantor.LocalAgent.Core.Modules;
using XKantor.LocalAgent.Monitors.Ipc;

namespace XKantor.LocalAgent.Monitors;

[SupportedOSPlatform("windows")]
public sealed class MonitorsModule : IAgentModule
{
    private static readonly TimeSpan MaksymalnyWiekRaportu = TimeSpan.FromSeconds(60);

    private readonly MonitorCache _cache;
    private readonly MonitorDiscoveryService _fallbackDiscovery;

    public MonitorsModule(MonitorCache cache, MonitorDiscoveryService fallbackDiscovery)
    {
        _cache = cache;
        _fallbackDiscovery = fallbackDiscovery;
    }

    public string Name => "Monitors";

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        if (_cache.TryGetFresh(MaksymalnyWiekRaportu, out var zRaportu) && zRaportu.Count > 0)
        {
            return zRaportu;
        }

        // Best-effort w procesie usługi (Session 0) - patrz ograniczenie opisane w
        // MonitorDiscoveryService.cs. Może zwrócić pustą listę, dopóki UserSession nie
        // podłączy się i nie przyśle prawdziwego raportu z sesji interaktywnej.
        return _fallbackDiscovery.Wykryj();
    }

    public Task<ModuleStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var monitory = GetMonitors();
        var zRaportuUserSession = _cache.TryGetFresh(MaksymalnyWiekRaportu, out _);

        if (monitory.Count == 0)
        {
            return Task.FromResult(new ModuleStatus
            {
                Name = Name,
                State = ModuleState.NotConfigured,
                Details = "Brak wykrytych monitorów - komponent XKantor.LocalAgent.UserSession prawdopodobnie nie jest uruchomiony w sesji operatora."
            });
        }

        var zrodlo = zRaportuUserSession ? "UserSession" : "wykrywanie w procesie usługi (Session 0)";
        return Task.FromResult(new ModuleStatus
        {
            Name = Name,
            State = ModuleState.Ready,
            Details = $"{monitory.Count} monitorów wykrytych (źródło: {zrodlo})."
        });
    }
}
