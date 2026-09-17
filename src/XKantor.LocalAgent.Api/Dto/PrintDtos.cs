using System.ComponentModel.DataAnnotations;
using XKantor.LocalAgent.Printing;

namespace XKantor.LocalAgent.Api.Dto;

// PRINT_TRANSACTION / PRINT_DOCUMENT (etap 2, sekcja 8/16/17) - żądanie wskazuje TYLKO
// docelową drukarkę (z listy zwróconej przez GET_PRINTERS) i gotowe dane, nigdy polecenie.
public sealed class PrintRequestDto
{
    [Required, MinLength(1), MaxLength(200)]
    public string PrinterName { get; set; } = "";

    public PrinterKind Kind { get; set; } = PrinterKind.Windows;

    [Required]
    public string ContentBase64 { get; set; } = "";

    [Required, MaxLength(200)]
    public string DocumentName { get; set; } = "dokument";

    // Tylko dla Kind=Windows (GDI, patrz WindowsDriverPrinter) - fizyczna szerokość rolki
    // papieru w mm (np. 57 albo 80), WŁĄCZNIE z marginesami. Sterownik domyślnie zakłada
    // A4/Letter (nie wie, że fizycznie to wąska rolka), więc bez tego tekst ląduje w lewym
    // górnym rogu ogromnej strony zamiast wypełniać całą szerokość rolki (zgłoszenie
    // użytkownika 2026-09-17). Puste/null = zachowanie sprzed tej zmiany (strona domyślna
    // sterownika, duży margines - sensowne dla zwykłych drukarek A4).
    [Range(20, 300)]
    public int? PaperWidthMm { get; set; }

    // Limit rozsądny dla paragonu/dokumentu tekstowego - chroni przed nadużyciem API do
    // wysyłania ogromnych danych binarnych (etap 2, sekcja 8: "walidacja/ograniczenia").
    public const int MaxContentBytes = 2 * 1024 * 1024;
}

public sealed record PrintResponseDto(bool Success, string? Error);
