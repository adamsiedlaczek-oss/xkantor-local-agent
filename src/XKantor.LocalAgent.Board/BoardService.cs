using XKantor.LocalAgent.Core.Configuration;

namespace XKantor.LocalAgent.Board;

// Warstwa logiki nad BoardConfigStore - waliduje, co przeglądarka próbuje zapisać (patrz
// Api/Endpoints/BoardEndpoints.cs), trzyma świeży stan w pamięci (żeby GET i pipe do
// UserSession - patrz Board/Ipc/BoardConfigPipeServer.cs - nie musiały czytać pliku za każdym
// razem). Stanowisko może mieć KILKA wpisów naraz (jeden na monitor) - Upsert dodaje/aktualizuje/
// usuwa TYLKO wpis dla jednego, wskazanego MonitorStableId, reszta listy zostaje nietknięta.
public sealed class BoardService
{
    private readonly BoardConfigStore _store;
    private readonly AgentConfig _agentConfig;
    private readonly object _blokada = new();
    private List<BoardConfig> _biezace;

    public BoardService(BoardConfigStore store, AgentConfig agentConfig)
    {
        _store = store;
        _agentConfig = agentConfig;
        _biezace = _store.ZaladujLubUtworz();
    }

    public IReadOnlyList<BoardConfig> GetConfigs()
    {
        lock (_blokada)
        {
            return _biezace;
        }
    }

    public (bool CzySukces, string? Blad) Upsert(string monitorStableId, bool enabled, string? monitorLabel, string? boardUrl)
    {
        if (string.IsNullOrWhiteSpace(monitorStableId))
        {
            return (false, "Brak wybranego monitora.");
        }

        if (enabled)
        {
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

        lock (_blokada)
        {
            var nowa = _biezace.Where(c => c.MonitorStableId != monitorStableId).ToList();
            if (enabled)
            {
                nowa.Add(new BoardConfig
                {
                    Enabled = true,
                    MonitorStableId = monitorStableId,
                    MonitorLabel = monitorLabel,
                    BoardUrl = boardUrl,
                    ConfiguredAtUtc = DateTimeOffset.UtcNow
                });
            }

            _biezace = nowa;
            _store.Zapisz(_biezace);
        }

        return (true, null);
    }

    private bool CzyDozwolonyOrigin(Uri uri)
    {
        var origin = $"{uri.Scheme}://{uri.Authority}";
        return _agentConfig.AllowedOrigins.Any(o => string.Equals(o.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
    }
}
