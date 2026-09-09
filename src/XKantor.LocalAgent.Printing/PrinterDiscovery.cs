using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace XKantor.LocalAgent.Printing;

// Wykrywanie drukarek Windows i portów LPT - patrz etap 2, sekcja 6 (moduł DEVICES) i 19
// (analogicznie do detekcji monitorów). Rozdzielone na osobną klasę (nie w IPrinter), żeby
// Devices/Api mogły wołać wykrywanie bez tworzenia konkretnej implementacji IPrinter.
[SupportedOSPlatform("windows")]
public static class PrinterDiscovery
{
    public static IReadOnlyList<PrinterInfo> WykryjDrukarkiWindows()
    {
        var domyslna = TryGetDefaultPrinter();
        var wynik = new List<PrinterInfo>();

        foreach (string nazwa in PrinterSettings.InstalledPrinters)
        {
            wynik.Add(new PrinterInfo(nazwa, PrinterKind.Windows, string.Equals(nazwa, domyslna, StringComparison.OrdinalIgnoreCase), IsAvailable: true));
        }

        return wynik;
    }

    private static string? TryGetDefaultPrinter()
    {
        try
        {
            var ustawienia = new PrinterSettings();
            return ustawienia.PrinterName;
        }
        catch
        {
            return null;
        }
    }

    // Sonduje obecność portów równoległych LPT1-LPT4 - CreateFile na "\\.\LPTn" zwraca
    // prawidłowy uchwyt tylko gdy port fizycznie/logicznie istnieje (typowa technika detekcji
    // portów na Windows, bez zależności od WMI).
    public static IReadOnlyList<PrinterInfo> WykryjPortyLpt()
    {
        var wynik = new List<PrinterInfo>();
        for (var i = 1; i <= 4; i++)
        {
            var port = $"LPT{i}";
            wynik.Add(new PrinterInfo(port, PrinterKind.Lpt, IsDefault: false, IsAvailable: PortIstnieje(port)));
        }
        return wynik;
    }

    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;
    private const int ErrorFileNotFound = 2;
    private const int ErrorAccessDenied = 5;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    private static bool PortIstnieje(string port)
    {
        using var uchwyt = CreateFile($@"\\.\{port}", GenericWrite, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (!uchwyt.IsInvalid) return true;

        // ERROR_ACCESS_DENIED (np. port zajęty przez inny proces) wciąż oznacza, że port
        // FIZYCZNIE istnieje - tylko ERROR_FILE_NOT_FOUND oznacza jego brak.
        var blad = Marshal.GetLastWin32Error();
        return blad == ErrorAccessDenied;
    }
}
