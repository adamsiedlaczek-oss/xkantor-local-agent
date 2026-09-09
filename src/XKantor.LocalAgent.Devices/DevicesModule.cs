using System.Runtime.Versioning;
using XKantor.LocalAgent.Core.Models;
using XKantor.LocalAgent.Core.Modules;

namespace XKantor.LocalAgent.Devices;

[SupportedOSPlatform("windows")]
public sealed class DevicesModule : IAgentModule
{
    private readonly DeviceDiscoveryService _discovery;

    public DevicesModule(DeviceDiscoveryService discovery)
    {
        _discovery = discovery;
    }

    public string Name => "Devices";

    public Task<ModuleStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var urzadzenia = _discovery.WykryjWszystkie();
        var dostepne = urzadzenia.Count(u => u.IsAvailable);

        return Task.FromResult(new ModuleStatus
        {
            Name = Name,
            State = ModuleState.Ready,
            Details = $"{dostepne}/{urzadzenia.Count} urządzeń dostępnych."
        });
    }
}
