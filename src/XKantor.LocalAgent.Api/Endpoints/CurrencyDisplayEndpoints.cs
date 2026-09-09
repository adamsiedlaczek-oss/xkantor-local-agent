using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using XKantor.LocalAgent.Api.Auth;
using XKantor.LocalAgent.Api.Dto;
using XKantor.LocalAgent.CurrencyDisplay;

namespace XKantor.LocalAgent.Api.Endpoints;

// UPDATE_CURRENCY_DISPLAY (etap 2, sekcja 8/18).
public static class CurrencyDisplayEndpoints
{
    public static void MapCurrencyDisplayEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/currency-display/update", async (UpdateCurrencyDisplayRequest request, CurrencyDisplayService service) =>
        {
            var kursy = request.Rates.Select(r => new CurrencyRate(r.Symbol, r.Buy, r.Sell)).ToList();
            var wynik = await service.UpdateAsync(kursy);
            return Results.Ok(new UpdateCurrencyDisplayResponse(wynik.CzySukces, wynik.Blad));
        }).RequireSessionScope("display");
    }
}
