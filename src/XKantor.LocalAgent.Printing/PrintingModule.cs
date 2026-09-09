using System.Runtime.Versioning;
using XKantor.LocalAgent.Core.Models;
using XKantor.LocalAgent.Core.Modules;

namespace XKantor.LocalAgent.Printing;

[SupportedOSPlatform("windows")]
public sealed class PrintingModule : IAgentModule
{
    public string Name => "Printing";

    public Task<ModuleStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var drukarki = PrinterDiscovery.WykryjDrukarkiWindows();
        var lpt = PrinterDiscovery.WykryjPortyLpt().Where(p => p.IsAvailable).ToList();

        if (drukarki.Count == 0 && lpt.Count == 0)
        {
            return Task.FromResult(new ModuleStatus { Name = Name, State = ModuleState.NotConfigured, Details = "Brak wykrytych drukarek Windows ani portów LPT." });
        }

        var szczegoly = $"{drukarki.Count} drukarek Windows, {lpt.Count} portów LPT dostępnych.";
        return Task.FromResult(new ModuleStatus { Name = Name, State = ModuleState.Ready, Details = szczegoly });
    }
}
