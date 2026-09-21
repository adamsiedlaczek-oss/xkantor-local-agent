namespace XKantor.LocalAgent.Board;

// Trwała KONFIGURACJA (intencja) tablicy kursów na drugim monitorze - "czy monitor jest teraz
// fizycznie podłączony" NIE jest tu trzymane (to liczy UserSession na żywo z Screen.AllScreens
// przy każdym cyklu watchdoga, nigdy nie zapisywane - patrz Board/Ipc/BoardConfigSnapshot.cs
// i UserSession/Board/KioskSupervisor.cs). Jedyny writer: Service, przez
// POST /api/v1/board/config (przeglądarka na stanowisku, sparowana - patrz BoardService.cs).
public sealed class BoardConfig
{
    public bool Enabled { get; set; }

    // Stabilny identyfikator monitora (patrz Monitors/MonitorStableIdResolver.cs) - NIE numer
    // kolejności.
    public string? MonitorStableId { get; set; }

    // Etykieta do UI (np. "Dell U2412M") - cache, może być null gdy EDID nierozpoznane.
    public string? MonitorLabel { get; set; }

    // Pełny adres /tablica/{CentralaId}/{KasaId} (kantorApp go liczy, Agent tylko przechowuje i
    // waliduje względem AgentConfig.AllowedOrigins - patrz BoardService.CzyDozwolonyOrigin).
    public string? BoardUrl { get; set; }

    public DateTimeOffset? ConfiguredAtUtc { get; set; }
}
