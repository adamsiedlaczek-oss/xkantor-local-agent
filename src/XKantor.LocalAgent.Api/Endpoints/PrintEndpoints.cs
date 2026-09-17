using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using XKantor.LocalAgent.Api.Auth;
using XKantor.LocalAgent.Api.Dto;
using XKantor.LocalAgent.Printing;

namespace XKantor.LocalAgent.Api.Endpoints;

// PRINT_TRANSACTION / PRINT_DOCUMENT (etap 2, sekcja 8/16/17) - oba wołają dokładnie tę samą,
// jawnie zdefiniowaną operację "wyślij te bajty na TĘ (zweryfikowaną) drukarkę" - w odróżnieniu
// od dwóch osobnych command name'ów w whitelist (Core/CommandWhitelist.cs), które istnieją
// głównie dla czytelności/audytu wywołań po stronie xkantor.app (paragon vs dokument ogólny).
public static class PrintEndpoints
{
    public static void MapPrintEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/print/transaction", Drukuj).RequireSessionScope("print");
        app.MapPost("/api/v1/print/document", Drukuj).RequireSessionScope("print");
    }

    private static async Task<IResult> Drukuj(PrintRequestDto request, ILogger<IPrinter> logger)
    {
        byte[] dane;
        try
        {
            dane = Convert.FromBase64String(request.ContentBase64);
        }
        catch (FormatException)
        {
            return Results.BadRequest(new PrintResponseDto(false, "ContentBase64 nie jest prawidłowym base64."));
        }

        if (dane.Length == 0 || dane.Length > PrintRequestDto.MaxContentBytes)
        {
            return Results.BadRequest(new PrintResponseDto(false, $"Rozmiar danych musi być w zakresie 1..{PrintRequestDto.MaxContentBytes} bajtów."));
        }

        // Whitelist celu drukowania: nazwa musi odpowiadać JEDNEJ z faktycznie wykrytych
        // drukarek/portów w danej chwili - Agent nigdy nie wysyła danych do dowolnej,
        // niezweryfikowanej nazwy podanej przez wywołującego (etap 2, sekcja 7/8).
        var dozwolone = request.Kind switch
        {
            PrinterKind.Lpt => PrinterDiscovery.WykryjPortyLpt().Where(p => p.IsAvailable).Select(p => p.Name),
            _ => PrinterDiscovery.WykryjDrukarkiWindows().Select(p => p.Name)
        };
        if (!dozwolone.Contains(request.PrinterName, StringComparer.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new PrintResponseDto(false, $"Drukarka/port '{request.PrinterName}' nie jest obecnie wykryty(a) na tym stanowisku."));
        }

        var drukarka = PrinterFactory.Utworz(request.PrinterName, request.Kind, request.PaperWidthMm);
        var wynik = await drukarka.DrukujAsync(dane, request.DocumentName);

        if (!wynik.CzySukces)
        {
            logger.LogWarning("Wydruk na '{Printer}' nie powiódł się: {Blad}", request.PrinterName, wynik.Blad);
        }

        return Results.Ok(new PrintResponseDto(wynik.CzySukces, wynik.Blad));
    }
}
