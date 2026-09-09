namespace XKantor.LocalAgent.Monitors.Ipc;

// Ostatni raport monitorów odebrany od XKantor.LocalAgent.UserSession przez named pipe
// (patrz MonitorPipeServer.cs) - trzymany w pamięci usługi, z znacznikiem czasu, żeby
// MonitorsModule mogło ocenić, czy dane są jeszcze "świeże" (patrz TryGetFresh).
public sealed class MonitorCache
{
    private readonly object _blokada = new();
    private IReadOnlyList<MonitorInfo> _ostatnie = Array.Empty<MonitorInfo>();
    private DateTimeOffset _ostatniaAktualizacjaUtc = DateTimeOffset.MinValue;

    public void Update(IReadOnlyList<MonitorInfo> monitory)
    {
        lock (_blokada)
        {
            _ostatnie = monitory;
            _ostatniaAktualizacjaUtc = DateTimeOffset.UtcNow;
        }
    }

    public bool TryGetFresh(TimeSpan maksymalnyWiek, out IReadOnlyList<MonitorInfo> monitory)
    {
        lock (_blokada)
        {
            if (DateTimeOffset.UtcNow - _ostatniaAktualizacjaUtc <= maksymalnyWiek)
            {
                monitory = _ostatnie;
                return true;
            }
        }

        monitory = Array.Empty<MonitorInfo>();
        return false;
    }
}
