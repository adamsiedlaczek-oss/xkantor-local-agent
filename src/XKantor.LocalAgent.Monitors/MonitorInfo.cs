namespace XKantor.LocalAgent.Monitors;

// Patrz etap 2, sekcja 19 (DETEKCJA MONITORÓW: "MONITOR ID, NAME, PRIMARY, RESOLUTION,
// POSITION, CONNECTED").
public sealed record MonitorInfo(
    string Id,
    string Name,
    bool IsPrimary,
    int WidthPx,
    int HeightPx,
    int PositionX,
    int PositionY);
