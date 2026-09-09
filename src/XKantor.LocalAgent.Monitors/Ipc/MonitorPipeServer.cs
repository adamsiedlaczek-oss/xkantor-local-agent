using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace XKantor.LocalAgent.Monitors.Ipc;

// Serwer named pipe uruchamiany przez XKantor.LocalAgent.Service (Session 0) - odbiera
// okresowe raporty monitorów od XKantor.LocalAgent.UserSession (Session N, sesja
// zalogowanego operatora) - patrz etap 2, sekcja 25 (SERVICE + IPC + USER SESSION AGENT) i
// docs/ARCHITECTURE.md. Nazwa pipe'a jest lokalna do maszyny (nie sieciowa).
[SupportedOSPlatform("windows")]
public sealed class MonitorPipeServer
{
    public const string PipeName = "XKantorLocalAgent.Monitors";

    private readonly MonitorCache _cache;

    public MonitorPipeServer(MonitorCache cache)
    {
        _cache = cache;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var pipe = UtworzPipe();
                await pipe.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(pipe);
                var linia = await reader.ReadLineAsync(ct);
                if (!string.IsNullOrWhiteSpace(linia))
                {
                    var monitory = JsonSerializer.Deserialize<List<MonitorInfo>>(linia);
                    if (monitory is not null) _cache.Update(monitory);
                }
            }
            catch (OperationCanceledException)
            {
                // Zamykanie usługi - normalne zakończenie pętli.
            }
            catch (Exception)
            {
                // Pojedyncze nieudane połączenie (np. UserSession padł w trakcie zapisu) nie
                // może zabić pętli serwera pipe'a - po prostu czekamy na kolejne połączenie.
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ContinueWith(_ => { }, TaskScheduler.Default);
            }
        }
    }

    // Uprawnienia pipe'a: zalogowani użytkownicy (Authenticated Users) mogą się połączyć i
    // pisać - usługa zwykle działa jako LocalSystem/NetworkService (inna sesja/konto niż
    // zalogowany operator), więc domyślne ACL named pipe'a (tylko właściciel) by go zablokowało.
    private static NamedPipeServerStream UtworzPipe()
    {
        var bezpieczenstwo = new PipeSecurity();
        bezpieczenstwo.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
            inBufferSize: 4096, outBufferSize: 4096, pipeSecurity: bezpieczenstwo);
    }
}
