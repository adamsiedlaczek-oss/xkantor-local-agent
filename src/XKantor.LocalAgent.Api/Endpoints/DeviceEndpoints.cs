using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using XKantor.LocalAgent.Api.Auth;
using XKantor.LocalAgent.Devices;
using XKantor.LocalAgent.Monitors;
using XKantor.LocalAgent.Printing;

namespace XKantor.LocalAgent.Api.Endpoints;

// GET_DEVICE_STATUS / GET_MONITORS / GET_PRINTERS (etap 2, sekcja 6/29) - wszystkie wymagają
// tokenu sesji z zakresem "read" (ujawniają informacje o sprzęcie stanowiska).
public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/devices", (DeviceDiscoveryService devices) => Results.Ok(devices.WykryjWszystkie()))
            .RequireSessionScope("read");

        app.MapGet("/api/v1/monitors", (MonitorsModule monitors) => Results.Ok(monitors.GetMonitors()))
            .RequireSessionScope("read");

        app.MapGet("/api/v1/printers", () =>
        {
            var windows = PrinterDiscovery.WykryjDrukarkiWindows();
            var lpt = PrinterDiscovery.WykryjPortyLpt();
            return Results.Ok(windows.Concat(lpt));
        }).RequireSessionScope("read");
    }
}
