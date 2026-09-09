using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using XKantor.LocalAgent.Core.Models;
using XKantor.LocalAgent.Core.Modules;
using XKantor.LocalAgent.Security;

namespace XKantor.LocalAgent.Api.Endpoints;

// GET_AGENT_STATUS (etap 2, sekcja 20) i health check szybki/jednoznaczny (sekcja 21) - oba
// BEZ wymogu tokenu sesji (status musi być czytelny zanim przeglądarka w ogóle sparuje/uzyska
// token), ale wciąż za Origin validation (patrz Middleware/OriginValidationMiddleware.cs -
// /health jest jedynym wyjątkiem, patrz komentarz tam).
public static class StatusEndpoints
{
    public static void MapStatusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", (IdentityService identity, RenewalPolicy polityka) =>
        {
            var status = identity.GetIdentityStatus(polityka);
            var stan = !status.CzySparowany || (!status.CertyfikatWazny && !status.WOkresieAwaryjnym)
                ? "OFFLINE"
                : status.WOkresieAwaryjnym ? "LIMITED" : "ONLINE";
            return Results.Text(stan, "text/plain");
        });

        app.MapGet("/api/v1/status", async (AgentStatusService statusService, IdentityService identity, RenewalPolicy polityka) =>
        {
            var identityStatus = identity.GetIdentityStatus(polityka);
            var raport = await statusService.ZbudujRaportAsync(identityStatus);
            return Results.Ok(raport);
        });
    }
}
