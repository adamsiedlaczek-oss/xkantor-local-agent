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
    int PositionY,
    // Etykieta do UI (np. "Dell U2412M"), rozpoznana z EDID (patrz MonitorStableIdResolver) -
    // null gdy się nie udało, wtedy UI pokazuje surowe Id z ostrzeżeniem.
    string? DisplayLabel = null);
