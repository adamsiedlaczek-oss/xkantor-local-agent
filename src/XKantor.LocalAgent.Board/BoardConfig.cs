namespace XKantor.LocalAgent.Board;

// Trwała KONFIGURACJA (intencja) JEDNEGO monitora tablicy kursów - stanowisko może mieć WIĘCEJ
// NIŻ JEDEN taki wpis naraz (kilka dodatkowych monitorów, każdy z własną tablicą/rotacją treści,
// patrz zadanie "checkbox musi pozwalać zaznaczać dowolną ilość monitorów") - patrz
// BoardConfigStore.cs, gdzie ta klasa jest elementem persystowanej List<BoardConfig>. "Czy
// monitor jest teraz fizycznie podłączony" NIE jest tu trzymane (to liczy UserSession na żywo z
// Screen.AllScreens przy każdym cyklu watchdoga, nigdy nie zapisywane - patrz
// UserSession/Board/KioskSupervisor.cs). Jedyny writer: Service, przez
// POST /api/v1/board/config (przeglądarka na stanowisku, sparowana - patrz BoardService.cs).
public sealed class BoardConfig
{
    // Zawsze true dla wpisu obecnego na liście - brak wpisu dla danego MonitorStableId = tablica
    // na nim wyłączona (patrz BoardService.Upsert). Pole zostaje (nie usuwamy z modelu), żeby nie
    // łamać kompatybilności JSON board.json ze starszą, jedno-monitorową wersją tego pliku.
    public bool Enabled { get; set; } = true;

    // Stabilny identyfikator monitora (patrz Monitors/MonitorStableIdResolver.cs) - NIE numer
    // kolejności. Klucz wpisu na liście.
    public string? MonitorStableId { get; set; }

    // Etykieta do UI (np. "Dell U2412M") - cache, może być null gdy EDID nierozpoznane.
    public string? MonitorLabel { get; set; }

    // Pełny adres /tablica/{CentralaId}/{KasaId}[?rot=N] (kantorApp go liczy, w tym ewentualną
    // rotację treści per monitor - Agent tylko przechowuje i waliduje względem
    // AgentConfig.AllowedOrigins, patrz BoardService.CzyDozwolonyOrigin).
    public string? BoardUrl { get; set; }

    public DateTimeOffset? ConfiguredAtUtc { get; set; }
}
