using System.ComponentModel.DataAnnotations;

namespace XKantor.LocalAgent.Api.Dto;

public sealed class CurrencyRateDto
{
    [Required, MaxLength(10)]
    public string Symbol { get; set; } = "";

    [Range(0, 1_000_000)]
    public decimal Buy { get; set; }

    [Range(0, 1_000_000)]
    public decimal Sell { get; set; }

    // Pezet\realizacja.txt sekcja 6A/17/18 - numer wiersza fizycznej tablicy (Waluta.PozycjaTablica
    // w xKantor.APP, UI "Pozycja wyświetlacz") - 0 = brak przypisanego wiersza (adapter PEZET
    // pomija). Adaptery bez pojęcia "wiersza" (SerialLineAdapter) po prostu tego nie czytają.
    [Range(0, 25)]
    public int DisplayRow { get; set; }

    // Sekcja 6C - liczba miejsc po przecinku (Waluta.MiejscPoPrzecinkuTablica) do formatowania
    // Buy/Sell przez adapter (np. Wysw8PezetAdapter).
    [Range(0, 8)]
    public int DecimalPlaces { get; set; } = 4;
}

public sealed class UpdateCurrencyDisplayRequest
{
    [Required, MinLength(1), MaxLength(50)]
    public List<CurrencyRateDto> Rates { get; set; } = new();
}

public sealed record UpdateCurrencyDisplayResponse(bool Success, string? Error);

// GET/SET konfiguracji urządzenia (zadanie pezet\realizacja.txt sekcja 12) - ten sam wzorzec co
// UPDATE_BOARD_CONFIG (Api/Dto/BoardDtos.cs), tylko dla wyświetlacza kursów zamiast monitora.
public sealed record CurrencyDisplayConfigDto(string AdapterType, string? Port, int BaudRate);

public sealed class SetCurrencyDisplayConfigRequest
{
    // "NONE" | "SERIAL_LINE" | "WYSW8_PEZET" - patrz CurrencyDisplayConfig.AdapterType.
    [Required, MaxLength(20)]
    public string AdapterType { get; set; } = "NONE";

    [MaxLength(20)]
    public string? Port { get; set; }

    [Range(300, 115200)]
    public int BaudRate { get; set; } = 9600;
}
