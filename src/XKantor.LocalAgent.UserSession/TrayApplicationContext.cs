using XKantor.LocalAgent.Monitors;
using XKantor.LocalAgent.Monitors.Ipc;
using XKantor.LocalAgent.UserSession.Board;

namespace XKantor.LocalAgent.UserSession;

// Komponent uruchamiany w SESJI ZALOGOWANEGO OPERATORA (nie jako usługa) - patrz etap 2,
// sekcja 24-25 ("SERVICE + USER SESSION COMPONENT"). Odpowiedzialności ograniczone do tego,
// co faktycznie wymaga interaktywnej sesji: detekcja monitorów (Screen.AllScreens - patrz
// Monitors/MonitorDiscoveryService.cs) i minimalne "LOCAL UI/STATUS" - ikona w zasobniku
// systemowym z bieżącym stanem, bez okna głównego (patrz Setup instrukcje w
// docs/INSTALLATION.md - autostart przez wpis w Harmonogramie zadań przy logowaniu).
public sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan OdstepRaportowania = TimeSpan.FromSeconds(20);

    private readonly NotifyIcon _ikona;
    private readonly MonitorDiscoveryService _discovery = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly System.Windows.Forms.Timer _tooltipTimer;
    private readonly CancellationTokenSource _cts = new();
    private readonly KioskSupervisor _kioskSupervisor = new();
    private string _statusMonitorow = "inicjalizacja...";

    public TrayApplicationContext()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("XKantor Local Agent - komponent sesji użytkownika", null).Enabled = false;
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Zakończ", null, (_, _) => ZamknijAplikacje());

        _ikona = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "XKantor Local Agent - inicjalizacja...",
            Visible = true,
            ContextMenuStrip = menu
        };

        _timer = new System.Windows.Forms.Timer { Interval = (int)OdstepRaportowania.TotalMilliseconds };
        _timer.Tick += async (_, _) => await RaportujAsync();
        _timer.Start();

        // Tooltip NotifyIcon.Text łączy status monitorów (raportowanie co 20s, wyżej) i status
        // tablicy kursów (KioskSupervisor, własny cykl 5s) - odświeżany częściej niż raport
        // monitorów, żeby operator widział aktualny stan watchdoga bez czekania 20s.
        _tooltipTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _tooltipTimer.Tick += (_, _) => OdswiezTooltip();
        _tooltipTimer.Start();

        _ = RaportujAsync();
    }

    private async Task RaportujAsync()
    {
        try
        {
            var monitory = _discovery.Wykryj();
            var wyslano = await MonitorPipeClient.WyslijAsync(monitory, _cts.Token);

            _statusMonitorow = wyslano
                ? $"{monitory.Count} monitor(ów), połączono z usługą."
                : $"{monitory.Count} monitor(ów), usługa niedostępna.";
        }
        catch
        {
            _statusMonitorow = "błąd wykrywania monitorów.";
        }

        OdswiezTooltip();
    }

    private void OdswiezTooltip()
    {
        // NotifyIcon.Text ma twardy limit 127 znaków w WinForms - łączymy zwięźle.
        var pelny = $"XKantor Local Agent - {_statusMonitorow}\n{_kioskSupervisor.StatusText}";
        _ikona.Text = pelny.Length > 127 ? pelny[..127] : pelny;
    }

    private void ZamknijAplikacje()
    {
        _cts.Cancel();
        _timer.Stop();
        _tooltipTimer.Stop();
        _kioskSupervisor.Dispose();
        _ikona.Visible = false;
        ExitThread();
    }
}
