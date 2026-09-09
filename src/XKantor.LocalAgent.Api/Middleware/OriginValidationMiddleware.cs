using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using XKantor.LocalAgent.Core.Configuration;

namespace XKantor.LocalAgent.Api.Middleware;

// Walidacja Origin/Referer - patrz etap 2, sekcja 15 ("Nie traktuj samego localhost jako
// pełnego zabezpieczenia... ochrona przed nieautoryzowanymi stronami internetowymi próbującymi
// komunikować się z Agentem"). Nasłuch na 127.0.0.1 (patrz Service/Program.cs) chroni przed
// dostępem SPOZA maszyny, ale NIE przed złośliwą stroną otwartą w tej samej przeglądarce,
// która spróbuje wywołać fetch() na localhost - stąd dodatkowa warstwa: Origin musi być na
// jawnej liście (AgentConfig.AllowedOrigins, patrz Core/Configuration/AgentConfig.cs).
public sealed class OriginValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OriginValidationMiddleware> _logger;

    public OriginValidationMiddleware(RequestDelegate next, ILogger<OriginValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AgentConfig config)
    {
        // /health celowo pomija walidację Origin - musi odpowiadać szybko i jednoznacznie
        // nawet zanim jakikolwiek Origin zostanie skonfigurowany (etap 2, sekcja 21), i nie
        // ujawnia żadnych danych stanowiska.
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var origin = context.Request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin) || !config.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Odrzucono żądanie {Path} - niedozwolony Origin '{Origin}'.", context.Request.Path, origin);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "origin_not_allowed" });
            return;
        }

        context.Response.Headers.AccessControlAllowOrigin = origin;
        context.Response.Headers.Vary = "Origin";

        await _next(context);
    }
}
