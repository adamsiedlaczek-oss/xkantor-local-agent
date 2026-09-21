using XKantor.LocalAgent.Core.Configuration;

namespace XKantor.LocalAgent.Board;

// Warstwa logiki nad BoardConfigStore - waliduje, co przeglądarka próbuje zapisać (patrz
// Api/Endpoints/BoardEndpoints.cs), trzyma świeży stan w pamięci (żeby GET i pipe do
// UserSession - patrz Board/Ipc/BoardConfigPipeServer.cs - nie musiały czytać pliku za każdym
// razem).
public sealed class BoardService
{
    private readonly BoardConfigStore _store;
    private readonly AgentConfig _agentConfig;
    private readonly object _blokada = new();
    private BoardConfig _biezacy;

    public BoardService(BoardConfigStore store, AgentConfig agentConfig)
    {
        _store = store;
        _agentConfig = agentConfig;
        _biezacy = _store.ZaladujLubUtworz();
    }

    public BoardConfig GetConfig()
    {
        lock (_blokada)
        {
            return _biezacy;
        }
    }

    public (bool CzySukces, string? Blad) Update(bool enabled, string? monitorStableId, string? monitorLabel, string? boardUrl)
    {
        if (enabled)
        {
            if (string.IsNullOrWhiteSpace(monitorStableId))
            {
                return (false, "Brak wybranego monitora.");
            }

            if (string.IsNullOrWhiteSpace(boardUrl) || !Uri.TryCreate(boardUrl, UriKind.Absolute, out var uri))
            {
                return (false, "Nieprawidłowy adres tablicy.");
            }

            // Agent nigdy nie uruchamia kiosku pod dowolny adres podany przez przeglądarkę -
            // ten sam wzorzec zaufania co OriginValidationMiddleware (AgentConfig.AllowedOrigins).
            if (!CzyDozwolonyOrigin(uri))
            {
                return (false, "Adres tablicy spoza dozwolonej listy originów tego stanowiska.");
            }
        }

        var nowy = new BoardConfig
        {
            Enabled = enabled,
            MonitorStableId = enabled ? monitorStableId : null,
            MonitorLabel = enabled ? monitorLabel : null,
            BoardUrl = enabled ? boardUrl : null,
            ConfiguredAtUtc = DateTimeOffset.UtcNow
        };

        lock (_blokada)
        {
            _biezacy = nowy;
        }

        _store.Zapisz(nowy);
        return (true, null);
    }

    private bool CzyDozwolonyOrigin(Uri uri)
    {
        var origin = $"{uri.Scheme}://{uri.Authority}";
        return _agentConfig.AllowedOrigins.Any(o => string.Equals(o.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
    }
}
