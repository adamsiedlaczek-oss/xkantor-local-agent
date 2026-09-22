using System.Text.Json;
using XKantor.LocalAgent.Core.Configuration;

namespace XKantor.LocalAgent.CurrencyDisplay;

// Ładuje/zapisuje currency-display.json - ten sam wzorzec co Core/Configuration/ConfigStore.cs
// (agent.json) i XKantor.LocalAgent.Board/BoardConfigStore.cs (board.json).
public sealed class CurrencyDisplayConfigStore
{
    private static readonly JsonSerializerOptions JsonOpcje = new() { WriteIndented = true };

    public CurrencyDisplayConfig ZaladujLubUtworz()
    {
        ConfigPaths.UpewnijSieZeFolderyIstnieja();

        if (!File.Exists(ConfigPaths.CurrencyDisplayConfigFile))
        {
            var domyslna = new CurrencyDisplayConfig();
            Zapisz(domyslna);
            return domyslna;
        }

        var json = File.ReadAllText(ConfigPaths.CurrencyDisplayConfigFile);
        try
        {
            return JsonSerializer.Deserialize<CurrencyDisplayConfig>(json) ?? new CurrencyDisplayConfig();
        }
        catch (JsonException)
        {
            // Uszkodzony plik - nigdy nie wywalaj startu Agenta, wracamy do NONE (zadanie
            // sekcja 10 - "błąd nie może zawiesić LocalAgent").
            return new CurrencyDisplayConfig();
        }
    }

    public void Zapisz(CurrencyDisplayConfig config)
    {
        ConfigPaths.UpewnijSieZeFolderyIstnieja();
        var json = JsonSerializer.Serialize(config, JsonOpcje);
        File.WriteAllText(ConfigPaths.CurrencyDisplayConfigFile, json);
    }
}
