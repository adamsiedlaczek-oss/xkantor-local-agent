using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using XKantor.LocalAgent.Api.Auth;
using XKantor.LocalAgent.Api.Dto;
using XKantor.LocalAgent.Board;

namespace XKantor.LocalAgent.Api.Endpoints;

// GET_BOARD_CONFIG / UPDATE_BOARD_CONFIG - konfiguracja tablicy kursów na drugim monitorze,
// wzorowane 1:1 na CurrencyDisplayEndpoints.cs. Adres tablicy (BoardUrl) liczy kantorApp
// (zna CentralaId/KasaId), Agent tylko przechowuje i waliduje origin - patrz BoardService.cs.
public static class BoardEndpoints
{
    public static void MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/board/config", (BoardService service) =>
        {
            var c = service.GetConfig();
            return Results.Ok(new BoardConfigResponse(c.Enabled, c.MonitorStableId, c.MonitorLabel, c.BoardUrl, c.ConfiguredAtUtc));
        }).RequireSessionScope("read");

        app.MapPost("/api/v1/board/config", (UpdateBoardConfigRequest request, BoardService service) =>
        {
            var (sukces, blad) = service.Update(request.Enabled, request.MonitorStableId, request.MonitorLabel, request.BoardUrl);
            if (!sukces)
            {
                return Results.BadRequest(new { error = blad });
            }

            var c = service.GetConfig();
            return Results.Ok(new BoardConfigResponse(c.Enabled, c.MonitorStableId, c.MonitorLabel, c.BoardUrl, c.ConfiguredAtUtc));
        }).RequireSessionScope("display");
    }
}
