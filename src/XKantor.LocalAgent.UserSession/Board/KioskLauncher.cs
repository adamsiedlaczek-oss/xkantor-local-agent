using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using XKantor.LocalAgent.Core.Configuration;
using XKantor.LocalAgent.Monitors;

namespace XKantor.LocalAgent.UserSession.Board;

// Uruchamia przeglądarkę w trybie kiosk, wycelowaną na konkretny monitor - patrz zadanie,
// sekcja 12/13. "--window-position"+"--kiosk" to standardowa technika kierowania okna
// fullscreen na wybrany monitor, ale nie zawsze w 100% honorowana - dlatego dodatkowo, PO
// starcie procesu, korygujemy pozycję jawnym SetWindowPos (belt-and-suspenders).
[SupportedOSPlatform("windows")]
public static class KioskLauncher
{
    public static Process? Uruchom(string przegladarkaExe, string url, MonitorInfo monitor)
    {
        var psi = new ProcessStartInfo(przegladarkaExe) { UseShellExecute = false };
        psi.ArgumentList.Add("--kiosk");
        psi.ArgumentList.Add(url);
        psi.ArgumentList.Add($"--window-position={monitor.PositionX},{monitor.PositionY}");
        psi.ArgumentList.Add($"--window-size={monitor.WidthPx},{monitor.HeightPx}");
        // Osobny, TRWAŁY profil (nie incognito) - odizolowany od profilu operatora, a trwałość
        // pomaga przy chwilowej utracie Internetu (cache przeglądarki), patrz zadanie sekcja 17.
        psi.ArgumentList.Add($"--user-data-dir={ConfigPaths.BoardProfileDir}");
        psi.ArgumentList.Add("--no-first-run");
        psi.ArgumentList.Add("--no-default-browser-check");
        psi.ArgumentList.Add("--disable-session-crashed-bubble");
        psi.ArgumentList.Add("--disable-infobars");
        psi.ArgumentList.Add("--noerrdialogs");
        psi.ArgumentList.Add("--disable-features=Translate");

        var proces = Process.Start(psi);
        if (proces is null)
        {
            return null;
        }

        _ = SkorygujPozycjeAsync(proces, monitor);
        return proces;
    }

    private static async Task SkorygujPozycjeAsync(Process proces, MonitorInfo monitor)
    {
        // Okno kiosku bywa tworzone z opóźnieniem (zwłaszcza przy pierwszym starcie nowego
        // profilu) - krótki retry zamiast jednorazowej próby.
        for (var proba = 0; proba < 15; proba++)
        {
            await Task.Delay(200);
            try
            {
                if (proces.HasExited) return;
                proces.Refresh();
                var uchwyt = proces.MainWindowHandle;
                if (uchwyt != IntPtr.Zero)
                {
                    SetWindowPos(uchwyt, IntPtr.Zero, monitor.PositionX, monitor.PositionY, monitor.WidthPx, monitor.HeightPx, SWP_NOZORDER | SWP_NOACTIVATE);
                    return;
                }
            }
            catch
            {
                return;
            }
        }
    }

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
