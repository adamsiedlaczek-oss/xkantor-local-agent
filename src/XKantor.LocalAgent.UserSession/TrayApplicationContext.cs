using XKantor.LocalAgent.Monitors;
using XKantor.LocalAgent.Monitors.Ipc;

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
    private readonly CancellationTokenSource _cts = new();

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

        _ = RaportujAsync();
    }

    private async Task RaportujAsync()
    {
        try
        {
            var monitory = _discovery.Wykryj();
            var wyslano = await MonitorPipeClient.WyslijAsync(monitory, _cts.Token);

            _ikona.Text = wyslano
                ? $"XKantor Local Agent - {monitory.Count} monitor(ów), połączono z usługą."
                : $"XKantor Local Agent - {monitory.Count} monitor(ów), usługa niedostępna.";
        }
        catch
        {
            _ikona.Text = "XKantor Local Agent - błąd wykrywania monitorów.";
        }
    }

    private void ZamknijAplikacje()
    {
        _cts.Cancel();
        _timer.Stop();
        _ikona.Visible = false;
        ExitThread();
    }
}
