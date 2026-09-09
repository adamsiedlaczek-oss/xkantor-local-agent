using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace XKantor.LocalAgent.Monitors.Ipc;

// Klient named pipe używany przez XKantor.LocalAgent.UserSession do przesyłania okresowych
// raportów monitorów do usługi (Session N -> Session 0) - patrz MonitorPipeServer.cs.
[SupportedOSPlatform("windows")]
public static class MonitorPipeClient
{
    public static async Task<bool> WyslijAsync(IReadOnlyList<MonitorInfo> monitory, CancellationToken ct, int timeoutMs = 2000)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(".", MonitorPipeServer.PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeoutMs, ct);

            var linia = JsonSerializer.Serialize(monitory) + "\n";
            var bajty = Encoding.UTF8.GetBytes(linia);
            await pipe.WriteAsync(bajty, ct);
            await pipe.FlushAsync(ct);
            return true;
        }
        catch (Exception)
        {
            // Usługa jeszcze niegotowa/nieuruchomiona - UserSession spróbuje ponownie przy
            // następnym cyklu (patrz UserSession/Program.cs), nie jest to błąd krytyczny.
            return false;
        }
    }
}
