using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;

namespace XKantor.LocalAgent.CurrencyDisplay;

// Sterownik fizycznej tablicy PEZET Opole - patrz pezet\realizacja.txt (kantorApp) i
// pezet\PEZET.BAT (golden reference legacy). NIE implementuje własnej komunikacji COM/USB -
// woła ISTNIEJĄCY, kompletny driver wysw8.exe (+ WYSW8.DLL, ten sam katalog) jako osobny,
// ukryty proces per wiersz tablicy:
//
//     wysw8.exe PORT WIERSZ KUPNO SPRZEDAZ
//
// PORT: "0".."9" (COM1..COM10, n = COM(n+1)) albo dosłownie "USB" - surowy token wysw8, ustawiony
// przez admina w konfiguracji Agenta (CurrencyDisplayConfig.Port), zadanie sekcja 12: "nie
// hardkoduj 3 tylko dlatego, że pezet.bat używa 3".
// WIERSZ: CurrencyRate.DisplayRow (Waluta.PozycjaTablica w xKantor.APP, etykieta UI "Pozycja
// wyświetlacz") - NIGDY kolejność w tabeli (zadanie sekcja 4/5).
// KUPNO/SPRZEDAZ: string.Format z kropką (CultureInfo.InvariantCulture), liczba miejsc po
// przecinku z CurrencyRate.DecimalPlaces (Waluta.MiejscPoPrzecinkuTablica) - zadanie sekcja 6C/20:
// zawsze prawdziwe zero ("0.0000"), NIGDY kropka-jako-flaga-braku-sprzedaży z legacy pezet.bat.
[SupportedOSPlatform("windows")]
public sealed class Wysw8PezetAdapter : ICurrencyDisplayAdapter
{
    private static readonly TimeSpan TimeoutNaWywolanie = TimeSpan.FromSeconds(5);

    private readonly string _port;
    private readonly string _katalogDrivera;

    public Wysw8PezetAdapter(string port)
        : this(port, Path.Combine(AppContext.BaseDirectory, "pezet"))
    {
    }

    // Konstruktor z jawnym katalogiem drivera - do testów (bez zależności od AppContext.BaseDirectory
    // procesu testowego). Produkcyjnie zawsze "<katalog instalacyjny Service>\pezet" (zadanie
    // sekcja 12/13 - żadnych ścieżek deweloperskich typu E:\adams\kantor\prog).
    public Wysw8PezetAdapter(string port, string katalogDrivera)
    {
        _port = port;
        _katalogDrivera = katalogDrivera;
    }

    public string Name => $"WYSW8_PEZET (port {_port})";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_port);

    public async Task<DisplayUpdateResult> UpdateAsync(IReadOnlyList<CurrencyRate> rates, CancellationToken ct = default)
    {
        if (!IsConfigured) return new DisplayUpdateResult(false, "Port tablicy PEZET nie jest skonfigurowany.");

        // DisplayRow <= 0 = ta waluta nie ma przypisanego wiersza tablicy PEZET (zadanie sekcja
        // 17/18) - pomijamy, NIE zgadujemy numeru i NIE nadpisujemy cudzego wiersza. Filtrujemy
        // PRZED sprawdzeniem obecności drivera - lista złożona wyłącznie z takich pozycji (np.
        // podgląd tablicy webowej tej samej Kasy) nie ma nic do wysłania, więc brak wysw8.exe
        // akurat teraz nie jest błędem.
        var doWyslania = rates.Where(r => r.DisplayRow > 0).ToList();
        if (doWyslania.Count == 0) return new DisplayUpdateResult(true, null);

        var sciezkaExe = Path.Combine(_katalogDrivera, "wysw8.exe");
        if (!File.Exists(sciezkaExe))
        {
            return new DisplayUpdateResult(false, $"Nie znaleziono sterownika PEZET (wysw8.exe) w '{_katalogDrivera}' - sprawdź instalację Agenta.");
        }

        // Sekwencyjnie, NIGDY równolegle (zadanie sekcja 11 - "nie uruchamiaj wielu wysw8.exe
        // naraz, może to powodować konflikt na COM/USB") - jeden fizyczny port, jeden proces na raz.
        var bledy = new List<string>();
        foreach (var kurs in doWyslania)
        {
            ct.ThrowIfCancellationRequested();

            var blad = await WyslijWierszAsync(sciezkaExe, kurs, ct);
            if (blad is not null) bledy.Add($"{kurs.Symbol} (wiersz {kurs.DisplayRow}): {blad}");
        }

        if (bledy.Count == 0) return new DisplayUpdateResult(true, null);
        return new DisplayUpdateResult(false, string.Join(" | ", bledy));
    }

    private async Task<string?> WyslijWierszAsync(string sciezkaExe, CurrencyRate kurs, CancellationToken ct)
    {
        var wzor = "0." + new string('0', Math.Max(0, kurs.DecimalPlaces));
        var kupno = kurs.Buy.ToString(wzor, CultureInfo.InvariantCulture);
        var sprzedaz = kurs.Sell.ToString(wzor, CultureInfo.InvariantCulture);

        var psi = new ProcessStartInfo(sciezkaExe)
        {
            WorkingDirectory = _katalogDrivera,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(_port);
        psi.ArgumentList.Add(kurs.DisplayRow.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(kupno);
        psi.ArgumentList.Add(sprzedaz);

        using var proces = new Process { StartInfo = psi };
        try
        {
            if (!proces.Start()) return "Nie udało się uruchomić wysw8.exe.";
        }
        catch (Exception ex)
        {
            return $"Błąd uruchomienia wysw8.exe: {ex.Message}";
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeoutNaWywolanie);
        try
        {
            await proces.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout (nie anulowanie z zewnątrz) - driver się zawiesił (np. czeka na urządzenie
            // na zajętym porcie) - zabijamy proces, żeby nie zostawić go wiszącego w tle
            // (zadanie sekcja 10: "błąd wysłania nie może zawiesić LocalAgent").
            TryKill(proces);
            return $"Przekroczono limit czasu ({TimeoutNaWywolanie.TotalSeconds:0}s) - sterownik nie odpowiedział.";
        }

        if (proces.ExitCode == 0) return null;

        var stderr = (await proces.StandardError.ReadToEndAsync()).Trim();
        var stdout = (await proces.StandardOutput.ReadToEndAsync()).Trim();
        var szczegoly = !string.IsNullOrEmpty(stderr) ? stderr : (!string.IsNullOrEmpty(stdout) ? stdout : "(brak szczegółów)");
        return $"kod {proces.ExitCode}: {szczegoly}";
    }

    private static void TryKill(Process proces)
    {
        try { if (!proces.HasExited) proces.Kill(entireProcessTree: true); }
        catch { /* proces mógł się właśnie zakończyć - nic do zrobienia */ }
    }
}
