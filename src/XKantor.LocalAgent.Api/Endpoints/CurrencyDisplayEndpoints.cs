using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using XKantor.LocalAgent.Api.Auth;
using XKantor.LocalAgent.Api.Dto;
using XKantor.LocalAgent.CurrencyDisplay;

namespace XKantor.LocalAgent.Api.Endpoints;

// UPDATE_CURRENCY_DISPLAY (etap 2, sekcja 8/18) + GET/SET konfiguracji urządzenia (zadanie
// pezet\realizacja.txt sekcja 12 - "jeżeli trzeba dodać konfigurację, dodaj ją zgodnie z
// istniejącym stylem Agenta" - wzorowane 1:1 na BoardEndpoints.cs/GET+POST board/config).
public static class CurrencyDisplayEndpoints
{
    public static void MapCurrencyDisplayEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/currency-display/update", async (UpdateCurrencyDisplayRequest request, CurrencyDisplayService service) =>
        {
            var kursy = request.Rates
                .Select(r => new CurrencyRate(r.Symbol, r.Buy, r.Sell, r.DisplayRow, r.DecimalPlaces))
                .ToList();
            var wynik = await service.UpdateAsync(kursy);
            return Results.Ok(new UpdateCurrencyDisplayResponse(wynik.CzySukces, wynik.Blad));
        }).RequireSessionScope("display");

        app.MapGet("/api/v1/currency-display/config", (CurrencyDisplayConfig config) =>
            Results.Ok(new CurrencyDisplayConfigDto(config.AdapterType, config.Port, config.BaudRate)))
            .RequireSessionScope("read");

        app.MapPost("/api/v1/currency-display/config", (SetCurrencyDisplayConfigRequest request, CurrencyDisplayConfig config, CurrencyDisplayConfigStore store) =>
        {
            var typ = (request.AdapterType ?? "NONE").Trim().ToUpperInvariant();
            if (typ is not ("NONE" or "SERIAL_LINE" or "WYSW8_PEZET"))
            {
                return Results.BadRequest(new { error = $"Nieznany typ adaptera: {request.AdapterType}" });
            }
            if (typ != "NONE" && string.IsNullOrWhiteSpace(request.Port))
            {
                return Results.BadRequest(new { error = "Port jest wymagany dla wybranego typu wyświetlacza." });
            }

            // Mutacja W MIEJSCU (nie nowy obiekt) - CurrencyDisplayConfig jest zarejestrowany jako
            // singleton REFERENCJA (Service/Program.cs), więc CurrencyDisplayService widzi zmianę
            // natychmiast, bez restartu Agenta.
            config.AdapterType = typ;
            config.Port = string.IsNullOrWhiteSpace(request.Port) ? null : request.Port.Trim();
            config.BaudRate = request.BaudRate;
            store.Zapisz(config);

            return Results.Ok(new CurrencyDisplayConfigDto(config.AdapterType, config.Port, config.BaudRate));
        }).RequireSessionScope("display");
    }
}
