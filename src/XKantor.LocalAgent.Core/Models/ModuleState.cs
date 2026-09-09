namespace XKantor.LocalAgent.Core.Models;

// Stan pojedynczego modułu sprzętowego (Printing/Devices/Monitors/CurrencyDisplay/Security) -
// patrz etap 2, sekcja 20 (przykład: "PRINTING: READY", "CURRENCY DISPLAY: NOT CONFIGURED").
public enum ModuleState
{
    Ready,
    NotConfigured,
    Degraded,
    Error,
    Disabled
}
