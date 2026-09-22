using System.Diagnostics;
using System.Runtime.Versioning;
using Serilog;
using XKantor.LocalAgent.Board;
using XKantor.LocalAgent.Board.Ipc;
using XKantor.LocalAgent.Monitors;

namespace XKantor.LocalAgent.UserSession.Board;

// Pilnuje tablic kursów na DOWOLNEJ liczbie dodatkowych monitorów naraz - własny timer (5s),
// OSOBNY od istniejącego 20s timera raportowania monitorów w TrayApplicationContext (różna
// pilność: tu decydujemy co sekundę-dwie, czy każdy kiosk ma żyć, tam tylko kosmetyczny raport).
// Patrz zadanie, sekcje 13/14/17: tablica ma przeżyć zamknięcie/crash/restart xKantor.APP (nie
// ma żadnej relacji rodzic-dziecko z przeglądarką kasjera - te procesy są uruchamiane WYŁĄCZNIE
// stąd), a watchdog ma restartować z ograniczonym, rosnącym opóźnieniem, nigdy w ciasnej pętli -
// PER MONITOR (jeden padający kiosk nie wpływa na backoff pozostałych).
[SupportedOSPlatform("windows")]
public sealed class KioskSupervisor : IDisposable
{
    private static readonly TimeSpan OdstepCyklu = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaksymalnyBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CzasUznaniaZaStabilne = TimeSpan.FromSeconds(30);

    private sealed class Instancja
    {
        public Process? Proces;
        public DateTime? ProcesUruchomionyUtc;
        public int KolejnaProbaBackoff;
        public DateTime? NastepnaProbaUtc;
        public string StatusText = "";
    }

    private readonly MonitorDiscoveryService _monitorDiscovery = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly CancellationTokenSource _cts = new();
    private readonly string? _przegladarkaExe;

    // Klucz: MonitorStableId. Wpis znika z tej mapy (po zatrzymaniu procesu), gdy dany monitor
    // przestaje być na liście włączonych configów (odznaczony checkbox / monitor odłączony).
    private readonly Dictionary<string, Instancja> _instancje = new();
    private List<BoardConfig> _ostatniZnaneConfigi = new();
    private bool _wTrakcieCyklu;

    public string StatusText { get; private set; } = "Tablica kursów: inicjalizacja...";

    public KioskSupervisor()
    {
        _przegladarkaExe = BrowserLocator.Znajdz();
        if (_przegladarkaExe is null)
        {
            StatusText = "Tablica kursów: brak zainstalowanej przeglądarki (Edge/Chrome).";
            Log.Warning("Board: nie znaleziono przeglądarki Edge/Chrome - kiosk tablicy nie może wystartować.");
        }

        _timer = new System.Windows.Forms.Timer { Interval = (int)OdstepCyklu.TotalMilliseconds };
        _timer.Tick += async (_, _) => await CykleAsync();
        _timer.Start();
    }

    private async Task CykleAsync()
    {
        // Poprzedni cykl mógł się jeszcze nie zakończyć (np. pipe wolniej odpowiada niż 5s) - nie
        // nakładamy się, po prostu czekamy na następny tick.
        if (_wTrakcieCyklu) return;
        _wTrakcieCyklu = true;

        try
        {
            var configi = await BoardConfigPipeClient.PobierzAsync(_cts.Token);
            if (configi is not null)
            {
                _ostatniZnaneConfigi = configi;
            }

            var aktywne = _ostatniZnaneConfigi
                .Where(c => c.Enabled && !string.IsNullOrWhiteSpace(c.MonitorStableId) && !string.IsNullOrWhiteSpace(c.BoardUrl))
                .ToList();

            // Wpisy usunięte z konfiguracji (odznaczony checkbox) od ostatniego cyklu - zamknij
            // ich procesy i wyrzuć z mapy, inaczej "osierocony" kiosk zostałby otwarty na zawsze.
            var aktywneId = aktywne.Select(c => c.MonitorStableId!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var usunietyId in _instancje.Keys.Where(id => !aktywneId.Contains(id)).ToList())
            {
                ZatrzymajIUsun(usunietyId, "Tablica wyłączona lub odznaczona.");
            }

            if (aktywne.Count == 0)
            {
                StatusText = "Tablica kursów: wyłączona.";
                return;
            }

            if (_przegladarkaExe is null)
            {
                StatusText = "Tablica kursów: brak zainstalowanej przeglądarki (Edge/Chrome).";
                return;
            }

            var monitory = _monitorDiscovery.Wykryj();
            foreach (var config in aktywne)
            {
                var docelowy = monitory.FirstOrDefault(m => m.Id == config.MonitorStableId);
                if (docelowy is null)
                {
                    // Monitor zniknął (odłączony/port zmieniony) - CICHY kill, bez tego okno
                    // kiosku mogłoby "wypłynąć" na inny monitor (patrz zadanie, sekcja 23).
                    ZatrzymajIUsun(config.MonitorStableId!, "Skonfigurowany monitor nie jest podłączony.");
                    continue;
                }

                // Świadomie BEZ blokady dla monitora Primary (ekran kasjera) - operator może chcieć
                // tablicę właśnie tam (decyzja użytkownika/operatora, nie nasza).
                await UpewnijSieZeDzialaAsync(config, docelowy);
            }

            StatusText = _instancje.Count == 1
                ? _instancje.Values.First().StatusText
                : $"Tablica kursów: {_instancje.Count} aktywnych ({string.Join("; ", _instancje.Values.Select(i => i.StatusText))}).";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Board: błąd w cyklu KioskSupervisor.");
        }
        finally
        {
            _wTrakcieCyklu = false;
        }
    }

    private async Task UpewnijSieZeDzialaAsync(BoardConfig config, MonitorInfo monitor)
    {
        if (!_instancje.TryGetValue(config.MonitorStableId!, out var instancja))
        {
            instancja = new Instancja();
            _instancje[config.MonitorStableId!] = instancja;
        }

        if (instancja.Proces is not null)
        {
            bool zakonczony;
            try
            {
                instancja.Proces.Refresh();
                zakonczony = instancja.Proces.HasExited;
            }
            catch
            {
                zakonczony = true;
            }

            if (!zakonczony)
            {
                if (instancja.ProcesUruchomionyUtc is not null && DateTime.UtcNow - instancja.ProcesUruchomionyUtc.Value >= CzasUznaniaZaStabilne)
                {
                    instancja.KolejnaProbaBackoff = 0;
                }

                instancja.StatusText = $"aktywna na {monitor.DisplayLabel ?? monitor.Id}";
                return;
            }

            Log.Information("Board: proces kiosku na {MonitorId} zakończył się nieoczekiwanie - watchdog zaplanuje restart.", monitor.Id);
            instancja.Proces = null;
            instancja.ProcesUruchomionyUtc = null;
            ZaplanujKolejnaProbe(instancja);
        }

        if (instancja.NastepnaProbaUtc is not null && DateTime.UtcNow < instancja.NastepnaProbaUtc.Value)
        {
            instancja.StatusText = $"oczekiwanie na restart na {monitor.DisplayLabel ?? monitor.Id} ({instancja.NastepnaProbaUtc.Value:HH:mm:ss})";
            return;
        }

        var proces = KioskLauncher.Uruchom(_przegladarkaExe!, config.BoardUrl!, monitor);
        if (proces is null)
        {
            Log.Warning("Board: nie udało się uruchomić procesu przeglądarki kiosku na {MonitorId}.", monitor.Id);
            ZaplanujKolejnaProbe(instancja);
            return;
        }

        instancja.Proces = proces;
        instancja.ProcesUruchomionyUtc = DateTime.UtcNow;
        instancja.NastepnaProbaUtc = null;
        instancja.StatusText = $"uruchomiono na {monitor.DisplayLabel ?? monitor.Id}";
        Log.Information("Board: uruchomiono kiosk (PID {Pid}) na monitorze {MonitorId} ({W}x{H} @ {X},{Y}).",
            proces.Id, monitor.Id, monitor.WidthPx, monitor.HeightPx, monitor.PositionX, monitor.PositionY);

        await Task.CompletedTask;
    }

    // Pułap 60s, nigdy nie poddaje się na stałe (patrz zadanie, sekcja 14: "ochrona przed
    // restart loop", ale bez trwałego stanu "zbyt wiele awarii - koniec") - PER instancja/monitor.
    private static void ZaplanujKolejnaProbe(Instancja instancja)
    {
        var sekundy = Math.Min(Math.Pow(2, instancja.KolejnaProbaBackoff), MaksymalnyBackoff.TotalSeconds);
        instancja.NastepnaProbaUtc = DateTime.UtcNow.AddSeconds(sekundy);
        instancja.KolejnaProbaBackoff++;
        Log.Information("Board: kolejna próba uruchomienia kiosku za {Sekundy}s.", sekundy);
    }

    private void ZatrzymajIUsun(string monitorStableId, string powod)
    {
        if (!_instancje.TryGetValue(monitorStableId, out var instancja)) return;

        try
        {
            if (instancja.Proces is { } p && !p.HasExited)
            {
                Log.Information("Board: zamykanie procesu kiosku na {MonitorId} ({Powod}).", monitorStableId, powod);
                p.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Proces mógł się zakończyć w międzyczasie - nic do zrobienia.
        }
        finally
        {
            _instancje.Remove(monitorStableId);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _timer.Stop();
        _timer.Dispose();
        foreach (var id in _instancje.Keys.ToList())
        {
            ZatrzymajIUsun(id, "Zamykanie UserSession.");
        }
        StatusText = "Tablica kursów: zatrzymana.";
    }
}
