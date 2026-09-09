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
}

public sealed class UpdateCurrencyDisplayRequest
{
    [Required, MinLength(1), MaxLength(50)]
    public List<CurrencyRateDto> Rates { get; set; } = new();
}

public sealed record UpdateCurrencyDisplayResponse(bool Success, string? Error);
