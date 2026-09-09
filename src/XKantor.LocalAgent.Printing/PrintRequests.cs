namespace XKantor.LocalAgent.Printing;

// Kontrakty żądań PRINT_TRANSACTION / PRINT_DOCUMENT (etap 2, sekcja 8/16) - żądanie nazywa
// TYLKO docelową drukarkę + gotowe dane do wysłania, nigdy polecenie systemowe.
public sealed record PrintTargetRequest(string PrinterName, PrinterKind Kind, string ContentBase64, string DocumentName);

public static class PrinterFactory
{
    public static IPrinter Utworz(string printerName, PrinterKind kind) => kind switch
    {
        PrinterKind.Raw => new RawWinspoolPrinter(printerName),
        PrinterKind.Lpt => new LptPrinter(printerName),
        PrinterKind.Windows => new WindowsDriverPrinter(printerName),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
