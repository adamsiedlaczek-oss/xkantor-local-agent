using XKantor.LocalAgent.Monitors.Ipc;

namespace XKantor.LocalAgent.Service;

// Uruchamia serwer named pipe (patrz Monitors/Ipc/MonitorPipeServer.cs) jako tło usługi -
// odbiera raporty monitorów od XKantor.LocalAgent.UserSession (patrz etap 2, sekcja 24-25).
public sealed class MonitorPipeHostedService : BackgroundService
{
    private readonly MonitorPipeServer _server;

    public MonitorPipeHostedService(MonitorPipeServer server)
    {
        _server = server;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => _server.RunAsync(stoppingToken);
}
