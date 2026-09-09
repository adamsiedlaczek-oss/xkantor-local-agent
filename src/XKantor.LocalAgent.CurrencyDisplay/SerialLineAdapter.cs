using System.IO.Ports;
using System.Runtime.Versioning;
using System.Text;

namespace XKantor.LocalAgent.CurrencyDisplay;

// Przykładowy, GENERYCZNY adapter po porcie szeregowym - wysyła prosty, czytelny protokół
// tekstowy (jedna linia na walutę: "SYMBOL;KUPNO;SPRZEDAZ\r\n"). NIE jest sterownikiem żadnego
// konkretnego producenta wyświetlacza LED (Eltin/Nautica/Hadex/...) - specyfikacji takiego
// urządzenia nie mamy (patrz etap 2, sekcja 18 i docs/QUESTIONS.csv #003). Służy jako
// referencyjna implementacja frameworku rozszerzeń: producent z prawdziwą specyfikacją
// protokołu dostanie WŁASNY adapter (np. EltinDisplayAdapter : ICurrencyDisplayAdapter)
// zarejestrowany obok tego w CurrencyDisplayService.
[SupportedOSPlatform("windows")]
public sealed class SerialLineAdapter : ICurrencyDisplayAdapter
{
    private readonly string _port;
    private readonly int _baudRate;

    public SerialLineAdapter(string port, int baudRate = 9600)
    {
        _port = port;
        _baudRate = baudRate;
    }

    public string Name => $"SERIAL_LINE ({_port})";
    public bool IsConfigured => true;

    public Task<DisplayUpdateResult> UpdateAsync(IReadOnlyList<CurrencyRate> rates, CancellationToken ct = default)
    {
        try
        {
            using var port = new SerialPort(_port, _baudRate) { NewLine = "\r\n", WriteTimeout = 2000 };
            port.Open();

            var sb = new StringBuilder();
            foreach (var r in rates)
            {
                sb.Append(r.Symbol).Append(';')
                  .Append(r.Buy.ToString("0.0000")).Append(';')
                  .Append(r.Sell.ToString("0.0000")).Append("\r\n");
            }

            port.Write(sb.ToString());
            return Task.FromResult(new DisplayUpdateResult(true, null));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new DisplayUpdateResult(false, ex.Message));
        }
    }
}
