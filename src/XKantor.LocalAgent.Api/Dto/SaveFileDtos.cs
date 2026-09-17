using System.ComponentModel.DataAnnotations;

namespace XKantor.LocalAgent.Api.Dto;

// SAVE_FILE (etap 2, sekcja 8/16) - żądanie niesie TYLKO nazwę pliku (nigdy ścieżkę) i gotowe
// dane - Agent SAM decyduje, GDZIE zapisać (patrz ConfigPaths.FilesDir), tak jak PRINT_TRANSACTION
// niesie tylko nazwę drukarki z whitelisty, nigdy dowolne polecenie/ścieżkę od wywołującego.
public sealed class SaveFileRequestDto
{
    [Required, MinLength(1), MaxLength(150)]
    public string FileName { get; set; } = "";

    [Required]
    public string ContentBase64 { get; set; } = "";

    public const int MaxContentBytes = 2 * 1024 * 1024;
}

public sealed record SaveFileResponseDto(bool Success, string? Error, string? SavedPath);
