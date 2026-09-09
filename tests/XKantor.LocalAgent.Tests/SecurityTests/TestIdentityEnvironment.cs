using XKantor.LocalAgent.Security;

namespace XKantor.LocalAgent.Tests.SecurityTests;

// Katalog tymczasowy dla testów Security - zamiast prawdziwego %ProgramData%\XKantorLocalAgent
// (patrz IdentityStore.cs - ścieżka pliku wstrzykiwana w konstruktorze). Wiele wywołań
// NowyIdentityService() w tym samym teście wskazuje na TEN SAM plik (symulacja restartu
// procesu z tym samym magazynem na dysku).
public sealed class TestIdentityEnvironment : IDisposable
{
    private readonly string _katalogTymczasowy;
    private readonly string _plikTozsamosci;

    public TestIdentityEnvironment()
    {
        _katalogTymczasowy = Path.Combine(Path.GetTempPath(), "xkantor-agent-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_katalogTymczasowy);
        _plikTozsamosci = Path.Combine(_katalogTymczasowy, "identity.bin");
    }

    public IdentityService NowyIdentityService() => new(new IdentityStore(_plikTozsamosci));

    public void Dispose()
    {
        try { Directory.Delete(_katalogTymczasowy, recursive: true); }
        catch { /* sprzątanie best-effort - katalog tymczasowy, nic krytycznego */ }
    }
}
