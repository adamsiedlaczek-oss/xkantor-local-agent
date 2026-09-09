using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using XKantor.LocalAgent.Api.Dto;
using XKantor.LocalAgent.Security;

namespace XKantor.LocalAgent.Api.Endpoints;

// Parowanie stanowiska - patrz etap 2, sekcja 11 i docs/PAIRING.md. Celowo BEZ wymogu tokenu
// sesji (parowanie jest tym, co token sesji dopiero umożliwia) - zabezpieczone Origin
// validation (Middleware/OriginValidationMiddleware.cs) + krótkim czasem życia/jednorazowością
// samego kodu parowania (Security/PairingService.cs).
public static class PairingEndpoints
{
    public static void MapPairingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/pairing/start", (PairingService pairing) =>
        {
            var wynik = pairing.StartPairing();
            return Results.Ok(new PairingStartResponse(
                wynik.PairingCode, wynik.ExpiresAtUtc,
                wynik.PublicKey.PublicKeySpkiBase64, wynik.PublicKey.Thumbprint, wynik.PublicKey.Algorithm));
        });

        app.MapPost("/api/v1/pairing/complete", (PairingCompleteRequest request, PairingService pairing, ILogger<PairingLogCategory> logger) =>
        {
            if (!pairing.ZweryfikujIZuzyjKod(request.PairingCode))
            {
                return Results.BadRequest(new { error = "invalid_or_expired_pairing_code" });
            }

            var wynik = pairing.CompletePairing(request.StationId, request.IssuerToken);
            logger.LogInformation("Stanowisko sparowane: StationId={StationId}.", request.StationId);

            return Results.Ok(new PairingCompleteResponse(request.StationId, wynik.StationSecretBase64, wynik.Certificate.ExpiresAtUtc));
        });
    }
}

// Marker wyłącznie do kategorii logowania (ILogger<T> nie akceptuje typów statycznych, a
// PairingEndpoints jest statyczna) - patrz podobne markery w pozostałych plikach Endpoints/*.cs.
internal sealed class PairingLogCategory;
