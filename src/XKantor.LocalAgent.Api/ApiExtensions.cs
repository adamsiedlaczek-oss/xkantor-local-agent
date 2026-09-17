using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using XKantor.LocalAgent.Api.Endpoints;
using XKantor.LocalAgent.Api.Middleware;
using XKantor.LocalAgent.Core.Configuration;
using XKantor.LocalAgent.Core.Modules;
using XKantor.LocalAgent.CurrencyDisplay;
using XKantor.LocalAgent.Devices;
using XKantor.LocalAgent.Monitors;
using XKantor.LocalAgent.Monitors.Ipc;
using XKantor.LocalAgent.Printing;
using XKantor.LocalAgent.Security;

namespace XKantor.LocalAgent.Api;

// Punkt składania Agenta (DI + routing) - wołane z Service/Program.cs. Osobna klasa (nie
// inline w Program.cs), żeby testy integracyjne (jeśli powstaną) mogły zbudować ten sam host
// bez duplikowania rejestracji.
public static class ApiExtensions
{
    public static IServiceCollection AddXKantorAgent(this IServiceCollection services, AgentConfig config, CurrencyDisplayConfig currencyDisplayConfig)
    {
        services.AddSingleton(config);
        services.AddSingleton(currencyDisplayConfig);
        services.AddSingleton<RenewalPolicy>();

        services.AddSingleton<IdentityStore>();
        services.AddSingleton<IdentityService>();
        services.AddSingleton<PairingService>();
        services.AddSingleton<RenewalService>();
        services.AddSingleton<SessionTokenService>();

        services.AddSingleton<DeviceDiscoveryService>();
        services.AddSingleton<MonitorCache>();
        services.AddSingleton<MonitorDiscoveryService>();
        services.AddSingleton<MonitorsModule>();
        services.AddSingleton<MonitorPipeServer>();
        services.AddSingleton<CurrencyDisplayService>();

        services.AddSingleton<IAgentModule>(sp => new PrintingModule());
        services.AddSingleton<IAgentModule>(sp => new DevicesModule(sp.GetRequiredService<DeviceDiscoveryService>()));
        services.AddSingleton<IAgentModule>(sp => sp.GetRequiredService<MonitorsModule>());
        services.AddSingleton<IAgentModule>(sp => new CurrencyDisplayModule(sp.GetRequiredService<CurrencyDisplayService>()));
        services.AddSingleton<AgentStatusService>();

        return services;
    }

    public static WebApplication UseXKantorAgent(this WebApplication app)
    {
        app.UseMiddleware<OriginValidationMiddleware>();

        app.MapStatusEndpoints();
        app.MapDeviceEndpoints();
        app.MapPrintEndpoints();
        app.MapSaveFileEndpoints();
        app.MapCurrencyDisplayEndpoints();
        app.MapPairingEndpoints();
        app.MapIdentityEndpoints();

        return app;
    }
}
