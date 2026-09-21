using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text.Json;

namespace XKantor.LocalAgent.Board.Ipc;

// Klient named pipe używany przez XKantor.LocalAgent.UserSession (KioskSupervisor) do
// odpytywania usługi o aktualny BoardConfig - patrz BoardConfigPipeServer.cs.
[SupportedOSPlatform("windows")]
public static class BoardConfigPipeClient
{
    private const string ZapytanieGet = "GET_BOARD_CONFIG";

    public static async Task<BoardConfig?> PobierzAsync(CancellationToken ct, int timeoutMs = 2000)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(".", BoardConfigPipeServer.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeoutMs, ct);

            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(ZapytanieGet);

            using var reader = new StreamReader(pipe, leaveOpen: true);
            var linia = await reader.ReadLineAsync(ct);
            return string.IsNullOrWhiteSpace(linia) ? null : JsonSerializer.Deserialize<BoardConfig>(linia);
        }
        catch (Exception)
        {
            // Service jeszcze niegotowa/nieuruchomiona - KioskSupervisor zachowuje poprzedni
            // znany stan i spróbuje ponownie przy następnym cyklu (nie jest to błąd krytyczny).
            return null;
        }
    }
}
