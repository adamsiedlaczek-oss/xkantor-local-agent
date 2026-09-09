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

    // Limit rozsądny dla paragonu/dokumentu tekstowego - chroni przed nadużyciem API do
    // wysyłania ogromnych danych binarnych (etap 2, sekcja 8: "walidacja/ograniczenia").
    public const int MaxContentBytes = 2 * 1024 * 1024;
}

public sealed record PrintResponseDto(bool Success, string? Error);
