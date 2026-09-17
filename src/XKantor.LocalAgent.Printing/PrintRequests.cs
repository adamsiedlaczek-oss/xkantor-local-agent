namespace XKantor.LocalAgent.Printing;

// Kontrakty żądań PRINT_TRANSACTION / PRINT_DOCUMENT (etap 2, sekcja 8/16) - żądanie nazywa
// TYLKO docelową drukarkę + gotowe dane do wysłania, nigdy polecenie systemowe.
public sealed record PrintTargetRequest(string PrinterName, PrinterKind Kind, string ContentBase64, string DocumentName);

public static class PrinterFactory
{
    // paperWidthMm - tylko dla PrinterKind.Windows (GDI), patrz WindowsDriverPrinter i komentarz
    // przy PrintRequestDto.PaperWidthMm. Ignorowane dla Raw/Lpt (te i tak wysyłają gotowe bajty
    // ESC/POS bez udziału GDI/sterownika - fizyczny rozmiar strony jest tam nieistotny).
    public static IPrinter Utworz(string printerName, PrinterKind kind, int? paperWidthMm = null) => kind switch
    {
        PrinterKind.Raw => new RawWinspoolPrinter(printerName),
        PrinterKind.Lpt => new LptPrinter(printerName),
        PrinterKind.Windows => new WindowsDriverPrinter(printerName, paperWidthMm),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
