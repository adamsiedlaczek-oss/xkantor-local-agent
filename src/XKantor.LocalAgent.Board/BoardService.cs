namespace XKantor.LocalAgent.Board;

// Warstwa logiki nad BoardConfigStore - waliduje, co przeglądarka próbuje zapisać (patrz
// Api/Endpoints/BoardEndpoints.cs), trzyma świeży stan w pamięci (żeby GET i pipe do
// UserSession - patrz Board/Ipc/BoardConfigPipeServer.cs - nie musiały czytać pliku za każdym
// razem). Stanowisko może mieć KILKA wpisów naraz (jeden na monitor) - Upsert dodaje/aktualizuje/
// usuwa TYLKO wpis dla jednego, wskazanego MonitorStableId, reszta listy zostaje nietknięta.
public sealed class BoardService
{
    private readonly BoardConfigStore _store;
    private readonly object _blokada = new();
    private List<BoardConfig> _biezace;

    public BoardService(BoardConfigStore store)
    {
        _store = store;
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
            if (string.IsNullOrWhiteSpace(boardUrl) || !Uri.TryCreate(boardUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return (false, "Nieprawidłowy adres tablicy (wymagany pełny adres http(s)://...).");
            }

            // ZMIANA (zadanie "WIDOK TABLICY", sekcja 3/7 - widoki typu URL, np. YouTube): dawniej
            // wymagaliśmy, żeby BoardUrl należał do AgentConfig.AllowedOrigins (ten sam wzorzec co
            // OriginValidationMiddleware) - to celowo uniemożliwiało kiosk pod DOWOLNYM adresem.
            // Teraz kantorApp ma jawny katalog "widoków" i administrator MOŻE świadomie wskazać
            // zewnętrzny URL - to już nie przypadkowy/nieautoryzowany adres z przeglądarki, tylko
            // intencjonalny wybór w panelu admina. Prawdziwą granicą zaufania jest sam token sesji
            // wymagany przez ten endpoint (scope "display", RequireSessionScope w BoardEndpoints.cs)
            // - wymaga wcześniejszego sparowania stanowiska (StationSecret), więc origin-allowlist
            // nie dawał tu dodatkowego bezpieczeństwa, tylko blokował zamierzoną funkcję.
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
}
