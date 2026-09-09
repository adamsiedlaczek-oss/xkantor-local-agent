namespace XKantor.LocalAgent.CurrencyDisplay;

// Adapter domyślny - żadne urządzenie nie jest jeszcze skonfigurowane na tym stanowisku.
// Nigdy nie udaje sukcesu (etap 2, sekcja 33: "nie udawaj implementacji").
public sealed class NotConfiguredAdapter : ICurrencyDisplayAdapter
{
    public string Name => "NOT_CONFIGURED";
    public bool IsConfigured => false;

    public Task<DisplayUpdateResult> UpdateAsync(IReadOnlyList<CurrencyRate> rates, CancellationToken ct = default) =>
        Task.FromResult(new DisplayUpdateResult(false, "Wyświetlacz kursów nie jest skonfigurowany na tym stanowisku."));
}
