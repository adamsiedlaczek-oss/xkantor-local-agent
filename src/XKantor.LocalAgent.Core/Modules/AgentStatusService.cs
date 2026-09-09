using XKantor.LocalAgent.Core.Configuration;
using XKantor.LocalAgent.Core.Models;

namespace XKantor.LocalAgent.Core.Modules;

// Agreguje status wszystkich modułów + tożsamości w jeden AgentStatusReport - patrz etap 2,
// sekcja 20 (STATUS AGENTA) i 21 (HEALTH CHECK). Nie zna szczegółów Security (żeby Core nie
// zależało od Security) - dostaje IdentityStatus gotowy z zewnątrz (patrz Api/Endpoints/StatusEndpoints.cs).
public sealed class AgentStatusService
{
    private readonly IReadOnlyList<IAgentModule> _moduly;
    private readonly AgentConfig _config;

    public AgentStatusService(IEnumerable<IAgentModule> moduly, AgentConfig config)
    {
        _moduly = moduly.ToList();
        _config = config;
    }

    public async Task<AgentStatusReport> ZbudujRaportAsync(IdentityStatus identity, CancellationToken ct = default)
    {
        var statusyModulow = new List<ModuleStatus>();
        foreach (var modul in _moduly)
        {
            if (!_config.JestModulWlaczony(modul.Name))
            {
                statusyModulow.Add(new ModuleStatus { Name = modul.Name, State = ModuleState.Disabled, Details = "Wyłączony w konfiguracji stanowiska." });
                continue;
            }

            try
            {
                statusyModulow.Add(await modul.GetStatusAsync(ct));
            }
            catch (Exception ex)
            {
                statusyModulow.Add(new ModuleStatus { Name = modul.Name, State = ModuleState.Error, Details = ex.Message });
            }
        }

        var overall = WyznaczOgolnyStan(identity, statusyModulow);

        return new AgentStatusReport
        {
            AgentVersion = _config.AgentVersion,
            OverallState = overall,
            Identity = identity,
            Modules = statusyModulow
        };
    }

    private static AgentOverallState WyznaczOgolnyStan(IdentityStatus identity, IReadOnlyList<ModuleStatus> moduly)
    {
        if (!identity.CzySparowany) return AgentOverallState.Offline;
        if (!identity.CertyfikatWazny && !identity.WOkresieAwaryjnym) return AgentOverallState.Offline;

        if (moduly.Any(m => m.State == ModuleState.Error)) return AgentOverallState.Limited;
        if (identity.WOkresieAwaryjnym) return AgentOverallState.Limited;

        return AgentOverallState.Online;
    }
}
