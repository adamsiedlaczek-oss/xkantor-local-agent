using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using XKantor.LocalAgent.Api.Auth;
using XKantor.LocalAgent.Api.Dto;
using XKantor.LocalAgent.Core.Configuration;

namespace XKantor.LocalAgent.Api.Endpoints;

// SAVE_FILE (etap 2, sekcja 8/16) - "Zapisz do pliku zamiast drukować" w xkantor.app, gdy
// operator potrzebuje kopii paragonu/raportu do kontroli (txt) na TYM stanowisku, zamiast na
// dysku serwera (co dotąd miało sens tylko przy instalacji lokalnej - patrz Data/Wydruki/
// GeneratorParagonu.ZapiszPlik w xkantor.app). Agent zapisuje WYŁĄCZNIE do jednego, ustalonego
// u siebie folderu (ConfigPaths.FilesDir) - wywołujący podaje TYLKO nazwę pliku, nigdy ścieżkę
// (etap 2, sekcja 7).
public static class SaveFileEndpoints
{
    public static void MapSaveFileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/files/save", Zapisz).RequireSessionScope("print");
    }

    private static async Task<IResult> Zapisz(SaveFileRequestDto request, ILogger<SaveFileRequestDto> logger)
    {
        byte[] dane;
        try
        {
            dane = Convert.FromBase64String(request.ContentBase64);
        }
        catch (FormatException)
        {
            return Results.BadRequest(new SaveFileResponseDto(false, "ContentBase64 nie jest prawidłowym base64.", null));
        }

        if (dane.Length == 0 || dane.Length > SaveFileRequestDto.MaxContentBytes)
        {
            return Results.BadRequest(new SaveFileResponseDto(false, $"Rozmiar danych musi być w zakresie 1..{SaveFileRequestDto.MaxContentBytes} bajtów.", null));
        }

        // Path.GetFileName odcina ewentualne komponenty ścieżki (np. "..\..\cokolwiek\plik.txt"
        // -> "plik.txt") - porównanie z oryginałem wykrywa, że coś takiego w ogóle przyszło, i
        // odrzuca żądanie zamiast po cichu "naprawiać" nazwę. GetInvalidFileNameChars dodatkowo
        // łapie znaki niedozwolone w nazwach plików Windows (np. ":", "*", "?").
        var nazwaPliku = Path.GetFileName(request.FileName);
        if (string.IsNullOrWhiteSpace(nazwaPliku)
            || nazwaPliku != request.FileName
            || nazwaPliku.Contains("..", StringComparison.Ordinal)
            || nazwaPliku.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return Results.BadRequest(new SaveFileResponseDto(false, "Nieprawidłowa nazwa pliku.", null));
        }

        try
        {
            Directory.CreateDirectory(ConfigPaths.FilesDir);
            var folderPelny = Path.GetFullPath(ConfigPaths.FilesDir);
            var sciezkaPelna = Path.GetFullPath(Path.Combine(folderPelny, nazwaPliku));

            // Obrona w głąb - na wypadek egzotycznych nazw (np. zarezerwowanych urządzeń Windows
            // "CON"/"NUL"/"PRN" albo trików Unicode), które mogłyby po normalizacji wyjść poza
            // FilesDir mimo przejścia walidacji wyżej.
            if (!sciezkaPelna.StartsWith(folderPelny + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new SaveFileResponseDto(false, "Nieprawidłowa nazwa pliku.", null));
            }

            await File.WriteAllBytesAsync(sciezkaPelna, dane);
            return Results.Ok(new SaveFileResponseDto(true, null, sciezkaPelna));
        }
        catch (Exception ex)
        {
            logger.LogWarning("Zapis pliku '{NazwaPliku}' nie powiódł się: {Blad}", nazwaPliku, ex.Message);
            return Results.Ok(new SaveFileResponseDto(false, ex.Message, null));
        }
    }
}
