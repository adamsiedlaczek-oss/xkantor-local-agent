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
        // ujawnia żadnych danych stanowiska. WAŻNE: pominięcie walidacji NIE MOŻE oznaczać
        // pominięcia samego nagłówka CORS - bez Access-Control-Allow-Origin przeglądarka i
        // tak zablokuje JS-owi odczyt odpowiedzi (żądanie sieciowo przechodzi, fetch() rzuca
        // błąd CORS) - odtworzone empirycznie: UstawieniaStrona.razor pokazywał "OFFLINE" mimo
        // Agenta faktycznie ONLINE, bo xkantorAgentHealth() w hardwareAgent.js łapał ten błąd.
        // Tu (w odróżnieniu od resztu endpointów) nie sprawdzamy AllowedOrigins - stąd "*"
        // zamiast odbicia konkretnego Origin, żeby zadziałało z KAŻDEJ strony (health nie
        // ujawnia danych stanowiska, więc to bezpieczne).
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            context.Response.Headers.AccessControlAllowOrigin = "*";

            // Private Network Access (PNA) - Chrome 104+ wysyła DODATKOWY preflight (nawet dla
            // zwykłego GET) gdy strona żyjąca w "publicznej" przestrzeni adresowej (https://
            // xkantor.app, serwer na VPS) próbuje dobić się do "lokalnej" (127.0.0.1, ten
            // Agent) - bez Access-Control-Allow-Private-Network: true w odpowiedzi na ten
            // preflight, fetch() rzuca "Failed to fetch" PRZED wysłaniem właściwego żądania.
            // Odtworzone na produkcji 2026-09-16 (działa z http://localhost, nie działa z
            // https://xkantor.app - różnica dokładnie w przestrzeni adresowej Origin).
            if (HttpMethods.IsOptions(context.Request.Method))
            {
                context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
                context.Response.Headers.AccessControlAllowMethods = "GET";
                context.Response.Headers.AccessControlAllowHeaders = "Content-Type, Authorization";
                context.Response.Headers["Access-Control-Max-Age"] = "600";
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

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

        // Preflight CORS (OPTIONS) - przeglądarka wysyła je automatycznie przed każdym
        // fetch() z Content-Type: application/json + nagłówkiem Authorization (to NIE jest
        // "simple request" wg specyfikacji Fetch/CORS), a żaden endpoint minimal API poniżej
        // nie mapuje metody OPTIONS - bez tej krótkiej odpowiedzi tutaj przeglądarka odrzuci
        // każde żądanie z xkantor.app zanim jeszcze dotrze do handlera (404/405, bez nagłówków
        // CORS). Krótkie spięcie PRZED _next(context) - dalej i tak nie ma czego wołać.
        // Access-Control-Allow-Private-Network - patrz komentarz w gałęzi /health wyżej, ten
        // sam mechanizm PNA dotyczy WSZYSTKICH żądań z https://xkantor.app (publiczna
        // przestrzeń adresowa) do tego Agenta (127.0.0.1, lokalna przestrzeń), nie tylko /health.
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
            context.Response.Headers.AccessControlAllowMethods = "GET, POST";
            context.Response.Headers.AccessControlAllowHeaders = "Content-Type, Authorization";
            context.Response.Headers["Access-Control-Max-Age"] = "600";
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await _next(context);
    }
}
