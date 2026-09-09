namespace XKantor.LocalAgent.CurrencyDisplay;

public sealed record DisplayUpdateResult(bool CzySukces, string? Blad);

// Kontrakt sterownika fizycznego wyświetlacza kursów - patrz etap 2, sekcja 18. Fundament pod
// przyszłe sterowniki konkretnych producentów (COM/RS232, USB, Ethernet) - na tym etapie
// dostarczamy TYLKO ten interfejs, NotConfiguredAdapter (domyślny) i SerialLineAdapter jako
// generyczny przykład protokołu tekstowego po porcie szeregowym (etap 2, sekcja 18: "Jeżeli
// brakuje specyfikacji sprzętu, utwórz poprawny framework rozszerzeń i oznacz urządzenie jako
// NOT_CONFIGURED" - żaden konkretny producent nie był podany, więc nie udajemy integracji z
// realnym urządzeniem, którego specyfikacji nie mamy - patrz docs/QUESTIONS.csv).
public interface ICurrencyDisplayAdapter
{
    string Name { get; }
    bool IsConfigured { get; }

    Task<DisplayUpdateResult> UpdateAsync(IReadOnlyList<CurrencyRate> rates, CancellationToken ct = default);
}
