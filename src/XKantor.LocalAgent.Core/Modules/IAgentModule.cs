using XKantor.LocalAgent.Core.Models;

namespace XKantor.LocalAgent.Core.Modules;

// Kontrakt wspólny dla modułów sprzętowych (Printing/Devices/Monitors/CurrencyDisplay) -
// pozwala AgentStatusService agregować ich stan bez znajomości szczegółów każdego modułu
// (patrz etap 2, sekcja 5: "architektura ma umożliwiać późniejsze dodawanie kolejnych
// modułów sprzętowych").
public interface IAgentModule
{
    string Name { get; }

    Task<ModuleStatus> GetStatusAsync(CancellationToken ct = default);
}
