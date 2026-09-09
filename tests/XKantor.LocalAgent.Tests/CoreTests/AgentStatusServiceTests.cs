using XKantor.LocalAgent.Core.Configuration;
using XKantor.LocalAgent.Core.Models;
using XKantor.LocalAgent.Core.Modules;

namespace XKantor.LocalAgent.Tests.CoreTests;

public sealed class AgentStatusServiceTests
{
    private sealed class FakeModule : IAgentModule
    {
        private readonly ModuleStatus _status;
        public string Name => _status.Name;
        public FakeModule(string name, ModuleState state) => _status = new ModuleStatus { Name = name, State = state };
        public Task<ModuleStatus> GetStatusAsync(CancellationToken ct = default) => Task.FromResult(_status);
    }

    private static readonly IdentityStatus Sparowany = new()
    {
        CzySparowany = true,
        StationId = "KANTOR-01",
        CertyfikatWazny = true,
        WOkresieAwaryjnym = false,
        WygasaUtc = DateTimeOffset.UtcNow.AddDays(20)
    };

    [Fact]
    public async Task NieSparowany_ZawszeOffline()
    {
        var service = new AgentStatusService(new[] { new FakeModule("Printing", ModuleState.Ready) }, new AgentConfig());
        var raport = await service.ZbudujRaportAsync(new IdentityStatus { CzySparowany = false, CertyfikatWazny = false, WOkresieAwaryjnym = false });

        Assert.Equal(AgentOverallState.Offline, raport.OverallState);
    }

    [Fact]
    public async Task SparowanyWszystkoGotowe_Online()
    {
        var service = new AgentStatusService(
            new IAgentModule[] { new FakeModule("Printing", ModuleState.Ready), new FakeModule("Monitors", ModuleState.NotConfigured) },
            new AgentConfig());

        var raport = await service.ZbudujRaportAsync(Sparowany);

        Assert.Equal(AgentOverallState.Online, raport.OverallState);
    }

    [Fact]
    public async Task ModulZBledem_Limited()
    {
        var service = new AgentStatusService(new IAgentModule[] { new FakeModule("Printing", ModuleState.Error) }, new AgentConfig());

        var raport = await service.ZbudujRaportAsync(Sparowany);

        Assert.Equal(AgentOverallState.Limited, raport.OverallState);
    }

    [Fact]
    public async Task WOkresieAwaryjnym_Limited()
    {
        var identity = Sparowany with { CertyfikatWazny = false, WOkresieAwaryjnym = true };
        var service = new AgentStatusService(new IAgentModule[] { new FakeModule("Printing", ModuleState.Ready) }, new AgentConfig());

        var raport = await service.ZbudujRaportAsync(identity);

        Assert.Equal(AgentOverallState.Limited, raport.OverallState);
    }

    [Fact]
    public async Task WygaslyPozaGrace_Offline()
    {
        var identity = Sparowany with { CertyfikatWazny = false, WOkresieAwaryjnym = false };
        var service = new AgentStatusService(new IAgentModule[] { new FakeModule("Printing", ModuleState.Ready) }, new AgentConfig());

        var raport = await service.ZbudujRaportAsync(identity);

        Assert.Equal(AgentOverallState.Offline, raport.OverallState);
    }

    [Fact]
    public async Task ModulWylaczonyWKonfiguracji_NieWplywaNaStanOgolny()
    {
        var config = new AgentConfig();
        config.EnabledModules["Printing"] = false;
        var service = new AgentStatusService(new IAgentModule[] { new FakeModule("Printing", ModuleState.Error) }, config);

        var raport = await service.ZbudujRaportAsync(Sparowany);

        Assert.Equal(AgentOverallState.Online, raport.OverallState);
        Assert.Equal(ModuleState.Disabled, raport.Modules.Single(m => m.Name == "Printing").State);
    }
}
