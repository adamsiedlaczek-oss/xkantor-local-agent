using System.Runtime.Versioning;
using System.Windows.Forms;

namespace XKantor.LocalAgent.Monitors;

// Wykrywanie monitorów - patrz etap 2, sekcja 19. Screen.AllScreens odczytuje konfigurację
// wyświetlaczy INTERAKTYWNEJ sesji, w której działa wywołujący proces.
//
// WAŻNE OGRANICZENIE WINDOWS (patrz etap 2, sekcja 24-25 i docs/DECISIONS.md): usługa Windows
// (XKantor.LocalAgent.Service) działa w Session 0, bez dostępu do sesji interaktywnej
// zalogowanego operatora - w tej sesji Screen.AllScreens może zwrócić pustą/nieaktualną listę.
// Dlatego architektura przewiduje osobny komponent XKantor.LocalAgent.UserSession,
// uruchamiany w sesji zalogowanego użytkownika, który wykonuje wykrywanie TUTAJ i przesyła
// wynik do usługi przez IPC (named pipe - patrz Core/Ipc, Service/AgentPipeServer.cs). Usługa,
// gdy UserSession nie jest jeszcze podłączony, best-effort próbuje wykryć monitory sama
// (może zwrócić 0 - to nie błąd, tylko środowiskowe ograniczenie Session 0, jawnie opisane w
// statusie modułu jako NotConfigured, nie fałszywie jako Ready z pustą listą).
[SupportedOSPlatform("windows")]
public sealed class MonitorDiscoveryService
{
    public IReadOnlyList<MonitorInfo> Wykryj()
    {
        return Screen.AllScreens.Select((ekran, indeks) =>
        {
            var rozpoznanie = MonitorStableIdResolver.Rozpoznaj(ekran.DeviceName, indeks);
            return new MonitorInfo(
                Id: rozpoznanie.StableId,
                Name: ekran.DeviceName,
                IsPrimary: ekran.Primary,
                WidthPx: ekran.Bounds.Width,
                HeightPx: ekran.Bounds.Height,
                PositionX: ekran.Bounds.X,
                PositionY: ekran.Bounds.Y,
                DisplayLabel: rozpoznanie.DisplayLabel);
        }).ToList();
    }
}
