using System.Diagnostics;
using System.Runtime.Versioning;
using Serilog;
using XKantor.LocalAgent.Board;
using XKantor.LocalAgent.Board.Ipc;
using XKantor.LocalAgent.Monitors;

namespace XKantor.LocalAgent.UserSession.Board;

// Pilnuje tablicy kursów na drugim monitorze - własny timer (5s), OSOBNY od istniejącego
// 20s timera raportowania monitorów w TrayApplicationContext (różna pilność: tu decydujemy co
// sekundę-dwie, czy kiosk ma żyć, tam tylko kosmetyczny raport). Patrz zadanie, sekcje 13/14/17:
// tablica ma przeżyć zamknięcie/crash/restart xKantor.APP (nie ma żadnej relacji rodzic-dziecko
// z przeglądarką kasjera - ten proces jest uruchamiany WYŁĄCZNIE stąd), a watchdog ma
// restartować z ograniczonym, rosnącym opóźnieniem, nigdy w ciasnej pętli.
[SupportedOSPlatform("windows")]
public sealed class KioskSupervisor : IDisposable
{
    private static readonly TimeSpan OdstepCyklu = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaksymalnyBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CzasUznaniaZaStabilne = TimeSpan.FromSeconds(30);

    private readonly MonitorDiscoveryService _monitorDiscovery = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly CancellationTokenSource _cts = new();
    private readonly string? _przegladarkaExe;

    private BoardConfig? _ostatniZnanyConfig;
    private Process? _procesKiosku;
    private DateTime? _procesUruchomionyUtc;
    private int _kolejnaProbaBackoff;
    private DateTime? _nastepnaProbaUtc;
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
        // Poprzedni cykl mógł się jeszcze nie zakończyć (np. SkorygujPozycjeAsync/pipe wolniej
        // odpowiada niż 5s) - nie nakładamy się, po prostu czekamy na następny tick.
        if (_wTrakcieCyklu) return;
        _wTrakcieCyklu = true;

        try
        {
            var config = await BoardConfigPipeClient.PobierzAsync(_cts.Token);
            if (config is not null)
            {
                _ostatniZnanyConfig = config;
            }

            var biezacy = _ostatniZnanyConfig;
            if (biezacy is null || !biezacy.Enabled || string.IsNullOrWhiteSpace(biezacy.MonitorStableId) || string.IsNullOrWhiteSpace(biezacy.BoardUrl))
            {
                ZatrzymajJesliDziala("Tablica kursów: wyłączona.");
                return;
            }

            if (_przegladarkaExe is null) return;

            var monitory = _monitorDiscovery.Wykryj();
            var docelowy = monitory.FirstOrDefault(m => m.Id == biezacy.MonitorStableId);

            if (docelowy is null)
            {
                // Monitor zniknął (odłączony/port zmieniony) - CICHY kill, bez tego okno kiosku
                // mogłoby "wypłynąć" na monitor kasjera (patrz zadanie, sekcja 23).
                ZatrzymajJesliDziala("Tablica kursów: skonfigurowany monitor nie jest podłączony.");
                return;
            }

            if (docelowy.IsPrimary)
            {
                // Zabezpieczenie twarde (sekcja 11) - primary mógł się zmienić od czasu
                // konfiguracji, sprawdzamy to co cykl, nie tylko przy pierwszym uruchomieniu.
                ZatrzymajJesliDziala("Tablica kursów: skonfigurowany monitor jest teraz głównym ekranem - wstrzymano.");
                Log.Warning("Board: monitor {MonitorId} jest teraz Primary - odmowa uruchomienia kiosku na ekranie kasjera.", docelowy.Id);
                return;
            }

            await UpewnijSieZeDzialaAsync(biezacy, docelowy);
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
        if (_procesKiosku is not null)
        {
            bool zakonczony;
            try
            {
                _procesKiosku.Refresh();
                zakonczony = _procesKiosku.HasExited;
            }
            catch
            {
                zakonczony = true;
            }

            if (!zakonczony)
            {
                if (_procesUruchomionyUtc is not null && DateTime.UtcNow - _procesUruchomionyUtc.Value >= CzasUznaniaZaStabilne)
                {
                    _kolejnaProbaBackoff = 0;
                }

                StatusText = $"Tablica kursów: aktywna na {monitor.DisplayLabel ?? monitor.Id}.";
                return;
            }

            Log.Information("Board: proces kiosku zakończył się nieoczekiwanie - watchdog zaplanuje restart.");
            _procesKiosku = null;
            _procesUruchomionyUtc = null;
            ZaplanujKolejnaProbe();
        }

        if (_nastepnaProbaUtc is not null && DateTime.UtcNow < _nastepnaProbaUtc.Value)
        {
            StatusText = $"Tablica kursów: oczekiwanie na restart ({_nastepnaProbaUtc.Value:HH:mm:ss})...";
            return;
        }

        var proces = KioskLauncher.Uruchom(_przegladarkaExe!, config.BoardUrl!, monitor);
        if (proces is null)
        {
            Log.Warning("Board: nie udało się uruchomić procesu przeglądarki kiosku.");
            ZaplanujKolejnaProbe();
            return;
        }

        _procesKiosku = proces;
        _procesUruchomionyUtc = DateTime.UtcNow;
        _nastepnaProbaUtc = null;
        StatusText = $"Tablica kursów: uruchomiono na {monitor.DisplayLabel ?? monitor.Id}.";
        Log.Information("Board: uruchomiono kiosk (PID {Pid}) na monitorze {MonitorId} ({W}x{H} @ {X},{Y}).",
            proces.Id, monitor.Id, monitor.WidthPx, monitor.HeightPx, monitor.PositionX, monitor.PositionY);

        await Task.CompletedTask;
    }

    // Pułap 60s, nigdy nie poddaje się na stałe (patrz zadanie, sekcja 14: "ochrona przed
    // restart loop", ale bez trwałego stanu "zbyt wiele awarii - koniec").
    private void ZaplanujKolejnaProbe()
    {
        var sekundy = Math.Min(Math.Pow(2, _kolejnaProbaBackoff), MaksymalnyBackoff.TotalSeconds);
        _nastepnaProbaUtc = DateTime.UtcNow.AddSeconds(sekundy);
        _kolejnaProbaBackoff++;
        Log.Information("Board: kolejna próba uruchomienia kiosku za {Sekundy}s.", sekundy);
    }

    private void ZatrzymajJesliDziala(string status)
    {
        StatusText = status;
        if (_procesKiosku is null) return;

        try
        {
            if (!_procesKiosku.HasExited)
            {
                Log.Information("Board: zamykanie procesu kiosku ({Powod}).", status);
                _procesKiosku.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Proces mógł się zakończyć w międzyczasie - nic do zrobienia.
        }
        finally
        {
            _procesKiosku = null;
            _procesUruchomionyUtc = null;
            _kolejnaProbaBackoff = 0;
            _nastepnaProbaUtc = null;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _timer.Stop();
        _timer.Dispose();
        ZatrzymajJesliDziala("Tablica kursów: zatrzymana.");
    }
}
