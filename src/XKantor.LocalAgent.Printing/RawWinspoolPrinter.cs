using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace XKantor.LocalAgent.Printing;

// Wysyłka gotowych bajtów bezpośrednio do kolejki wydruku Windows w trybie RAW - klasyczny
// wzorzec "RawPrinterHelper" (Microsoft KB322091), P/Invoke do winspool.drv. Analogiczna
// implementacja już istnieje i działa produkcyjnie w xkantor.app (Data/Wydruki/
// RawPrinterService.cs) - ten Agent dostaje WŁASNĄ kopię (osobne repo, etap 2 sekcja 2:
// "Nie zmieniaj żadnego istniejącego projektu xkantor.app"), ale ten sam, sprawdzony wzorzec.
[SupportedOSPlatform("windows")]
public sealed class RawWinspoolPrinter : IPrinter
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool OpenPrinter(string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, DOCINFOA di);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, byte[] pBytes, int dwCount, out int dwWritten);

    public string Nazwa { get; }

    public RawWinspoolPrinter(string nazwaDrukarkiWindows)
    {
        Nazwa = nazwaDrukarkiWindows;
    }

    public Task<PrintResult> DrukujAsync(byte[] dane, string nazwaDokumentu, CancellationToken ct = default)
    {
        if (!OpenPrinter(Nazwa, out var hPrinter, IntPtr.Zero))
        {
            return Task.FromResult(new PrintResult(false, $"Nie udało się otworzyć drukarki '{Nazwa}' (błąd Win32 {Marshal.GetLastWin32Error()})."));
        }

        try
        {
            var docInfo = new DOCINFOA { pDocName = nazwaDokumentu, pDataType = "RAW" };
            if (!StartDocPrinter(hPrinter, 1, docInfo))
            {
                return Task.FromResult(new PrintResult(false, $"StartDocPrinter nie powiodło się (błąd Win32 {Marshal.GetLastWin32Error()})."));
            }

            try
            {
                if (!StartPagePrinter(hPrinter))
                {
                    return Task.FromResult(new PrintResult(false, $"StartPagePrinter nie powiodło się (błąd Win32 {Marshal.GetLastWin32Error()})."));
                }

                try
                {
                    if (!WritePrinter(hPrinter, dane, dane.Length, out var zapisano) || zapisano != dane.Length)
                    {
                        return Task.FromResult(new PrintResult(false, $"WritePrinter zapisało {zapisano}/{dane.Length} bajtów (błąd Win32 {Marshal.GetLastWin32Error()})."));
                    }
                }
                finally
                {
                    EndPagePrinter(hPrinter);
                }
            }
            finally
            {
                EndDocPrinter(hPrinter);
            }

            return Task.FromResult(new PrintResult(true, null));
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }
}
