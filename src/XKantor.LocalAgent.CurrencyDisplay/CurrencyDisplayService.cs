using System.Runtime.Versioning;

namespace XKantor.LocalAgent.CurrencyDisplay;

[SupportedOSPlatform("windows")]
public sealed class CurrencyDisplayService
{
    private readonly CurrencyDisplayConfig _config;

    public CurrencyDisplayService(CurrencyDisplayConfig config)
    {
        _config = config;
    }

    public ICurrencyDisplayAdapter ZbudujAdapter() => _config.AdapterType switch
    {
        "SERIAL_LINE" when !string.IsNullOrWhiteSpace(_config.Port) => new SerialLineAdapter(_config.Port, _config.BaudRate),
        _ => new NotConfiguredAdapter()
    };

    public Task<DisplayUpdateResult> UpdateAsync(IReadOnlyList<CurrencyRate> rates, CancellationToken ct = default) =>
        ZbudujAdapter().UpdateAsync(rates, ct);
}
