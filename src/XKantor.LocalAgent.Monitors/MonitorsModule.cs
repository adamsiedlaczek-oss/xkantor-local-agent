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

    public MonitorsModule(MonitorCache cache)
    {
        _cache = cache;
    }

    public string Name => "Monitors";

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        // WCZEŚNIEJ było tu wywoływane MonitorDiscoveryService.Wykryj() usługi jako "best-effort
        // fallback" - ale usługa działa w Session 0 (patrz MonitorDiscoveryService.cs), gdzie
        // Screen.AllScreens NIE zwraca pustej listy, tylko SYNTETYCZNY pulpit Session 0 - w
        // praktyce dokładnie JEDEN fikcyjny ekran 1024x768 ("FALLBACK:MONITOR1" po przejściu
        // przez MonitorStableIdResolver, bo nie ma EDID/interfejsu do rozpoznania). To myląco
        // wyglądało jak prawdziwy, wykryty monitor operatora zamiast jasno sygnalizować "UserSession
        // jeszcze się nie podłączył". Jedyne wiarygodne źródło to raport z UserSession przez pipe
        // (MonitorPipeServer/MonitorCache) - bez świeżego raportu zwracamy pustą listę.
        return _cache.TryGetFresh(MaksymalnyWiekRaportu, out var zRaportu) ? zRaportu : Array.Empty<MonitorInfo>();
    }

    public Task<ModuleStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var monitory = GetMonitors();

        if (monitory.Count == 0)
        {
            return Task.FromResult(new ModuleStatus
            {
                Name = Name,
                State = ModuleState.NotConfigured,
                Details = "Brak wykrytych monitorów - komponent XKantor.LocalAgent.UserSession prawdopodobnie nie jest uruchomiony w sesji operatora (albo jeszcze nie przesłał pierwszego raportu - do 20s po starcie)."
            });
        }

        return Task.FromResult(new ModuleStatus
        {
            Name = Name,
            State = ModuleState.Ready,
            Details = $"{monitory.Count} monitorów wykrytych (źródło: UserSession)."
        });
    }
}
