using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using XKantor.LocalAgent.Security;

namespace XKantor.LocalAgent.Api.Auth;

// Endpoint filter (minimal API) - wymaga ważnego, krótkoterminowego tokenu sesji z właściwym
// zakresem w nagłówku Authorization: Bearer - patrz etap 2, sekcja 14 i
// Security/SessionTokenService.cs. Stosowany na wszystkich whitelisted komendach poza
// GET_AGENT_STATUS/health i parowaniem (patrz Endpoints/*.cs - RequireSessionScope()).
public sealed class SessionAuthFilter : IEndpointFilter
{
    private readonly string _wymaganyZakres;

    public SessionAuthFilter(string wymaganyZakres)
    {
        _wymaganyZakres = wymaganyZakres;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var tokenService = context.HttpContext.RequestServices.GetRequiredService<SessionTokenService>();

        var naglowek = context.HttpContext.Request.Headers.Authorization.ToString();
        var token = naglowek.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? naglowek["Bearer ".Length..]
            : null;

        var (wynik, _) = tokenService.Waliduj(token, _wymaganyZakres);
        if (wynik != SessionTokenValidationResult.Valid)
        {
            return Results.Json(new { error = "unauthorized", reason = wynik.ToString() }, statusCode: StatusCodes.Status401Unauthorized);
        }

        return await next(context);
    }
}

public static class SessionAuthFilterExtensions
{
    public static RouteHandlerBuilder RequireSessionScope(this RouteHandlerBuilder builder, string scope) =>
        builder.AddEndpointFilter(new SessionAuthFilter(scope));
}
