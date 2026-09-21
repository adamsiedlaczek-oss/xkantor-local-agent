using System.Net;
using Serilog;
using XKantor.LocalAgent.Api;
using XKantor.LocalAgent.Core.Configuration;
using XKantor.LocalAgent.CurrencyDisplay;
using XKantor.LocalAgent.Security;
using XKantor.LocalAgent.Service;

// Konfiguracja PUBLICZNA (Core/Configuration/AgentConfig.cs) - ładowana/tworzona PRZED
// zbudowaniem hosta, bo decyduje m.in. o porcie Kestrela (patrz niżej).
var configStore = new ConfigStore();
var agentConfig = configStore.ZaladujLubUtworz();
var currencyDisplayConfig = new CurrencyDisplayConfig(); // TODO(docs/QUESTIONS.csv #003): własny plik konfiguracyjny per-urządzenie, gdy pojawi się pierwszy prawdziwy sterownik.

ConfigPaths.UpewnijSieZeFolderyIstnieja();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    // Rotacja dzienna + limit plików (etap 2, sekcja 22: "Dodaj mechanizm rotacji logów") -
    // Serilog.Sinks.File z rollingInterval/retainedFileCountLimit, bez własnej, ręcznej logiki.
    .WriteTo.File(Path.Combine(ConfigPaths.LogsDir, "agent-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("XKantor Local Hardware Agent {Version} - start.", agentConfig.AgentVersion);

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // Windows Service - patrz etap 2, sekcja 24. Gdy proces NIE jest uruchomiony jako usługa
    // (np. `dotnet run` podczas developmentu), UseWindowsService() jest no-opem - ten sam
    // Program.cs obsługuje oba scenariusze bez rozgałęzień.
    builder.Host.UseWindowsService(opts => opts.ServiceName = "XKantorLocalAgent");

    // Agent NIGDY nie nasłuchuje na 0.0.0.0 - wyłącznie loopback (etap 2, sekcja 9). Jawne
    // ConfigureKestrel (nie appsettings.json) - żeby ta decyzja nie mogła zostać po cichu
    // nadpisana przez konfigurację środowiskową.
    builder.WebHost.ConfigureKestrel(kestrel =>
    {
        kestrel.Listen(IPAddress.Loopback, agentConfig.ListenPort);
    });

    builder.Services.AddXKantorAgent(agentConfig, currencyDisplayConfig);
    builder.Services.AddHostedService<RenewalWatcherHostedService>();
    builder.Services.AddHostedService<MonitorPipeHostedService>();
    builder.Services.AddHostedService<BoardConfigPipeHostedService>();

    var app = builder.Build();

    // Inicjalizacja tożsamości (generuje parę kluczy przy pierwszym starcie) MUSI nastąpić
    // przed przyjęciem pierwszego żądania - status/parowanie/sesje zakładają, że klucz już
    // istnieje (patrz Security/IdentityService.cs).
    app.Services.GetRequiredService<IdentityService>().EnsureKeyPair();

    app.UseXKantorAgent();

    Log.Information("Lokalny API nasłuchuje na http://127.0.0.1:{Port} (WYŁĄCZNIE loopback).", agentConfig.ListenPort);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "XKantor Local Hardware Agent zakończył się błędem krytycznym przy starcie.");
}
finally
{
    Log.CloseAndFlush();
}
