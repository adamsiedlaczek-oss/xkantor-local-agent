namespace XKantor.LocalAgent.Printing;

public sealed record PrintResult(bool CzySukces, string? Blad);

// Abstrakcja drukowania - patrz etap 2, sekcja 17 ("Zaprojektuj warstwę abstrakcji drukowania,
// np. IPrinter"). Implementacje: WindowsPrinter (kolejka Windows, sterownik), RawPrinter
// (RAW przez winspool.drv - kody ESC/POS/PCL bezpośrednio), LptPrinter (port równoległy).
// Żadna implementacja nie przyjmuje dowolnych poleceń systemowych - tylko gotowe bajty do
// wysłania (etap 2, sekcja 7/17).
public interface IPrinter
{
    string Nazwa { get; }

    Task<PrintResult> DrukujAsync(byte[] dane, string nazwaDokumentu, CancellationToken ct = default);
}
