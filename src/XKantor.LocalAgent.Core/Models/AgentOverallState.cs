namespace XKantor.LocalAgent.Core.Models;

// Ogólny stan Agenta zwracany przez /api/v1/status i /health - patrz etap 2, sekcja 6 (STATUS) i 21 (HEALTH CHECK).
public enum AgentOverallState
{
    // Wszystkie moduły gotowe (albo świadomie NotConfigured/Disabled), tożsamość ważna.
    Online,

    // Agent działa, ale co najmniej jeden moduł ma problem, lub tożsamość jest w okresie
    // awaryjnym (grace period) po wygaśnięciu certyfikatu - patrz etap 2, sekcja 13.
    Limited,

    // Certyfikat wygasł poza grace period, albo Agent nie jest jeszcze sparowany.
    Offline,

    // Błąd uniemożliwiający normalną pracę (np. nie udało się załadować/wygenerować tożsamości).
    Error
}
