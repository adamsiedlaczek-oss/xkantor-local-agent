using System.Text.Json;
using XKantor.LocalAgent.Core.Configuration;

namespace XKantor.LocalAgent.Board;

// Ładuje/zapisuje board.json - ten sam wzorzec co Core/Configuration/ConfigStore.cs
// (agent.json), celowo osobny plik/klasa (jak CurrencyDisplayConfig), żeby nie mieszać
// niezwiązanych configów w jednym pliku/typie.
public sealed class BoardConfigStore
{
    private static readonly JsonSerializerOptions JsonOpcje = new() { WriteIndented = true };

    public BoardConfig ZaladujLubUtworz()
    {
        ConfigPaths.UpewnijSieZeFolderyIstnieja();

        if (!File.Exists(ConfigPaths.BoardConfigFile))
        {
            var domyslny = new BoardConfig();
            Zapisz(domyslny);
            return domyslny;
        }

        var json = File.ReadAllText(ConfigPaths.BoardConfigFile);
        return JsonSerializer.Deserialize<BoardConfig>(json) ?? new BoardConfig();
    }

    public void Zapisz(BoardConfig config)
    {
        ConfigPaths.UpewnijSieZeFolderyIstnieja();
        var json = JsonSerializer.Serialize(config, JsonOpcje);
        File.WriteAllText(ConfigPaths.BoardConfigFile, json);
    }
}
