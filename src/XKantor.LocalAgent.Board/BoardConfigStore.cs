using System.Text.Json;
using XKantor.LocalAgent.Core.Configuration;

namespace XKantor.LocalAgent.Board;

// Ładuje/zapisuje board.json - ten sam wzorzec co Core/Configuration/ConfigStore.cs
// (agent.json), celowo osobny plik/klasa (jak CurrencyDisplayConfig), żeby nie mieszać
// niezwiązanych configów w jednym pliku/typie. Plik trzyma TABLICĘ wpisów (jeden na monitor,
// patrz BoardConfig.cs) - wczesniejsza, jedno-monitorowa wersja trzymała pojedynczy obiekt, stąd
// fallback w ZaladujLubUtworz przy deserializacji starego formatu.
public sealed class BoardConfigStore
{
    private static readonly JsonSerializerOptions JsonOpcje = new() { WriteIndented = true };

    public List<BoardConfig> ZaladujLubUtworz()
    {
        ConfigPaths.UpewnijSieZeFolderyIstnieja();

        if (!File.Exists(ConfigPaths.BoardConfigFile))
        {
            var pusta = new List<BoardConfig>();
            Zapisz(pusta);
            return pusta;
        }

        var json = File.ReadAllText(ConfigPaths.BoardConfigFile);
        return OdczytajListeAlboStaryFormat(json);
    }

    public void Zapisz(List<BoardConfig> configi)
    {
        ConfigPaths.UpewnijSieZeFolderyIstnieja();
        var json = JsonSerializer.Serialize(configi, JsonOpcje);
        File.WriteAllText(ConfigPaths.BoardConfigFile, json);
    }

    // Stary board.json (wersja jedno-monitorowa) zaczynał się od "{", nowy (lista) od "[" - jeśli
    // ktoś aktualizuje Agenta bez przechodzenia przez świeżą instalację, pierwszy odczyt po
    // aktualizacji trafi na stary format. Migrujemy w locie (pojedynczy wpis -> lista jednego
    // elementu, ale tylko gdy faktycznie był Enabled+skonfigurowany).
    private static List<BoardConfig> OdczytajListeAlboStaryFormat(string json)
    {
        var przycieta = json.TrimStart();
        if (przycieta.StartsWith('['))
        {
            return JsonSerializer.Deserialize<List<BoardConfig>>(json) ?? new List<BoardConfig>();
        }

        var stary = JsonSerializer.Deserialize<BoardConfig>(json);
        if (stary is { Enabled: true, MonitorStableId: not null })
        {
            return new List<BoardConfig> { stary };
        }

        return new List<BoardConfig>();
    }
}
