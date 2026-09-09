namespace XKantor.LocalAgent.CurrencyDisplay;

// Konfiguracja urządzenia wyświetlacza kursów tego stanowiska - część AgentConfig w praktyce,
// trzymana osobno tutaj żeby Core nie musiało znać typów specyficznych dla tego modułu.
public sealed class CurrencyDisplayConfig
{
    // "NONE" (domyślnie - NotConfiguredAdapter) albo "SERIAL_LINE" (SerialLineAdapter).
    public string AdapterType { get; set; } = "NONE";
    public string? Port { get; set; }
    public int BaudRate { get; set; } = 9600;
}
