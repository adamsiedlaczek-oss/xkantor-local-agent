using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using XKantor.LocalAgent.Api.Auth;
using XKantor.LocalAgent.Api.Dto;
using XKantor.LocalAgent.Security;

namespace XKantor.LocalAgent.Api.Endpoints;

// Sesja (etap 2, sekcja 14) i odnawianie tożsamości (sekcja 12-13) - patrz
// Security/SessionTokenService.cs i Security/RenewalService.cs dla pełnego uzasadnienia
// projektowego (Agent nigdy nie łączy się z Internetem - przeglądarka jest przekaźnikiem).
public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/session/start", (SessionStartRequest request, SessionTokenService tokens) =>
        {
            var token = tokens.WystawToken(request.StationSecretBase64, request.Scopes);
            if (token is null)
            {
                return Results.Json(new { error = "invalid_station_secret_or_not_paired" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Ok(new SessionStartResponse(token, DateTimeOffset.UtcNow.AddMinutes(2)));
        });

        app.MapPost("/api/v1/identity/renewal-request", (RenewalService renewal) =>
        {
            try
            {
                var zadanie = renewal.CreateRenewalRequest();
                return Results.Ok(new RenewalRequestResponse(zadanie.PayloadJson, zadanie.SignatureBase64));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireSessionScope("renew");

        app.MapPost("/api/v1/identity/certificate", (InstallCertificateRequest request, RenewalService renewal) =>
        {
            try
            {
                renewal.InstallRenewedCertificate(new StationCertificate
                {
                    StationId = request.StationId,
                    PublicKeyThumbprint = request.PublicKeyThumbprint,
                    IssuedAtUtc = request.IssuedAtUtc,
                    ExpiresAtUtc = request.ExpiresAtUtc,
                    IssuerToken = request.IssuerToken
                });
                return Results.Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireSessionScope("renew");
    }
}
