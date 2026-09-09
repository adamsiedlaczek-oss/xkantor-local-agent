using System.Text.Json;

namespace XKantor.LocalAgent.Core.Configuration;

// Ładuje/zapisuje agent.json (konfiguracja PUBLICZNA, patrz AgentConfig.cs). Jawny tekst -
// nic wrażliwego tu nie trafia. Tworzy plik z wartościami domyślnymi przy pierwszym uruchomieniu.
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOpcje = new() { WriteIndented = true };

    public AgentConfig ZaladujLubUtworz()
    {
        ConfigPaths.UpewnijSieZeFolderyIstnieja();

        if (!File.Exists(ConfigPaths.AgentConfigFile))
        {
            var domyslna = new AgentConfig();
            Zapisz(domyslna);
            return domyslna;
        }

        var json = File.ReadAllText(ConfigPaths.AgentConfigFile);
        return JsonSerializer.Deserialize<AgentConfig>(json) ?? new AgentConfig();
    }

    public void Zapisz(AgentConfig config)
    {
        ConfigPaths.UpewnijSieZeFolderyIstnieja();
        var json = JsonSerializer.Serialize(config, JsonOpcje);
        File.WriteAllText(ConfigPaths.AgentConfigFile, json);
    }
}
