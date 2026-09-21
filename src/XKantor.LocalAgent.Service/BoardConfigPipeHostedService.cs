using XKantor.LocalAgent.Board.Ipc;

namespace XKantor.LocalAgent.Service;

// Uruchamia serwer named pipe "XKantorLocalAgent.Board" jako tło usługi - odpowiada
// UserSession/Board/KioskSupervisor.cs na pytania o aktualny BoardConfig. Mirror
// MonitorPipeHostedService.cs (kierunek pipe'a jest przeciwny, patrz komentarz w
// BoardConfigPipeServer.cs).
public sealed class BoardConfigPipeHostedService : BackgroundService
{
    private readonly BoardConfigPipeServer _server;

    public BoardConfigPipeHostedService(BoardConfigPipeServer server)
    {
        _server = server;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => _server.RunAsync(stoppingToken);
}
