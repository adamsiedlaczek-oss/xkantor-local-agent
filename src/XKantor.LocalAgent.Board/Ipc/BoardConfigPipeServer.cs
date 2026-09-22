using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace XKantor.LocalAgent.Board.Ipc;

// Serwer named pipe uruchamiany przez XKantor.LocalAgent.Service (Session 0) - w PRZECIWNYM
// kierunku niż Monitors/Ipc/MonitorPipeServer.cs: tam UserSession WYSYŁA dane do Service, tu
// UserSession PYTA Service o aktualny BoardConfig (Service jest jedynym writerem board.json,
// patrz BoardConfigStore.cs - UserSession nigdy nie czyta/pisze pliku bezpośrednio). Jedno
// zapytanie/odpowiedź na połączenie, bez długo utrzymywanego strumienia - ten sam styl
// odporności na pojedyncze nieudane połączenie co MonitorPipeServer.
[SupportedOSPlatform("windows")]
public sealed class BoardConfigPipeServer
{
    public const string PipeName = "XKantorLocalAgent.Board";
    private const string ZapytanieGet = "GET_BOARD_CONFIG";

    private readonly BoardService _service;

    public BoardConfigPipeServer(BoardService service)
    {
        _service = service;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var pipe = UtworzPipe();
                await pipe.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(pipe, leaveOpen: true);
                using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

                var zadanie = await reader.ReadLineAsync(ct);
                if (string.Equals(zadanie, ZapytanieGet, StringComparison.Ordinal))
                {
                    var json = JsonSerializer.Serialize(_service.GetConfigs());
                    await writer.WriteLineAsync(json);
                }
            }
            catch (OperationCanceledException)
            {
                // Zamykanie usługi - normalne zakończenie pętli.
            }
            catch (Exception)
            {
                // Pojedyncze nieudane połączenie (np. UserSession padł w trakcie odczytu) nie
                // może zabić pętli serwera pipe'a.
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ContinueWith(_ => { }, TaskScheduler.Default);
            }
        }
    }

    // Ta sama ACL co MonitorPipeServer.UtworzPipe() - AuthenticatedUsers ReadWrite, bo usługa
    // działa na innym koncie/sesji niż zalogowany operator.
    private static NamedPipeServerStream UtworzPipe()
    {
        var bezpieczenstwo = new PipeSecurity();
        bezpieczenstwo.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
            inBufferSize: 4096, outBufferSize: 4096, pipeSecurity: bezpieczenstwo);
    }
}
