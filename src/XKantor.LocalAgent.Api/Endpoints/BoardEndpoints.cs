using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using XKantor.LocalAgent.Api.Auth;
using XKantor.LocalAgent.Api.Dto;
using XKantor.LocalAgent.Board;

namespace XKantor.LocalAgent.Api.Endpoints;

// GET_BOARD_CONFIG / UPDATE_BOARD_CONFIG - konfiguracja tablicy kursów na dowolnej liczbie
// dodatkowych monitorów, wzorowane 1:1 na CurrencyDisplayEndpoints.cs (poza tym, że tu jest
// LISTA wpisów, nie jeden). Adres tablicy (BoardUrl) liczy kantorApp (zna CentralaId/KasaId,
// ewentualną rotację treści jako "?rot="), Agent tylko przechowuje i waliduje origin - patrz
// BoardService.cs. POST robi UPSERT jednego monitora (Enabled=false = usuń jego wpis) i zwraca
// za każdym razem pełną, zaktualizowaną listę - przeglądarka nie musi robić osobnego GET po POST.
public static class BoardEndpoints
{
    public static void MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/board/config", (BoardService service) =>
            Results.Ok(DoOdpowiedzi(service))).RequireSessionScope("read");

        app.MapPost("/api/v1/board/config", (UpdateBoardConfigRequest request, BoardService service) =>
        {
            var (sukces, blad) = service.Upsert(request.MonitorStableId, request.Enabled, request.MonitorLabel, request.BoardUrl);
            if (!sukces)
            {
                return Results.BadRequest(new { error = blad });
            }

            return Results.Ok(DoOdpowiedzi(service));
        }).RequireSessionScope("display");
    }

    private static List<BoardConfigEntryResponse> DoOdpowiedzi(BoardService service) =>
        service.GetConfigs()
            .Select(c => new BoardConfigEntryResponse(c.MonitorStableId!, c.MonitorLabel, c.BoardUrl, c.ConfiguredAtUtc))
            .ToList();
}
