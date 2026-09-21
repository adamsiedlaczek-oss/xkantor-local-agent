using System.Runtime.Versioning;
using Microsoft.Win32;

namespace XKantor.LocalAgent.UserSession.Board;

// Znajduje już zainstalowaną przeglądarkę obsługującą tryb kiosk (Edge, potem Chrome) - żadnego
// nowego oprogramowania (patrz zadanie, sekcja 12: "Wykorzystaj istniejącą przeglądarkę"). Klucz
// rejestru "App Paths" to standardowa, udokumentowana konwencja Windows do lokalizowania .exe
// zarejestrowanych aplikacji - nie wymaga uprawnień administratora do odczytu, działa niezależnie
// od miejsca instalacji.
[SupportedOSPlatform("windows")]
public static class BrowserLocator
{
    public static string? Znajdz()
    {
        return ZTraseRejestru("msedge.exe")
            ?? ZTraseRejestru("chrome.exe")
            ?? ZeSciezkiDomyslnej("msedge.exe", @"Microsoft\Edge\Application")
            ?? ZeSciezkiDomyslnej("chrome.exe", @"Google\Chrome\Application");
    }

    private static string? ZTraseRejestru(string exe)
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var klucz = hive.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exe}");
                var sciezka = klucz?.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(sciezka) && File.Exists(sciezka))
                {
                    return sciezka;
                }
            }
            catch
            {
                // Brak klucza / brak dostępu - próbujemy kolejnego źródła.
            }
        }

        return null;
    }

    private static string? ZeSciezkiDomyslnej(string exe, string podkatalog)
    {
        foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            var sciezka = Path.Combine(Environment.GetFolderPath(folder), podkatalog, exe);
            if (File.Exists(sciezka))
            {
                return sciezka;
            }
        }

        return null;
    }
}
