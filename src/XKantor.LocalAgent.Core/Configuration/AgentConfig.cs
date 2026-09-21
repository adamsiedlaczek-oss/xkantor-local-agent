namespace XKantor.LocalAgent.Core.Configuration;

// Konfiguracja PUBLICZNA Agenta - żadnych sekretów (patrz etap 2, sekcja 28: "Rozdziel PUBLIC
// CONFIGURATION od SECURE SECRETS"). Klucz prywatny/certyfikat/station secret żyją osobno,
// zaszyfrowane DPAPI - patrz Security/SecureKeyStore.cs i Security/IdentityStore.cs.
public sealed class AgentConfig
{
    public const string CurrentVersion = "1.0.0";

    public string AgentVersion { get; set; } = CurrentVersion;

    // Port lokalnego API - nasłuch WYŁĄCZNIE na 127.0.0.1 (patrz etap 2, sekcja 9), nigdy 0.0.0.0.
    public int ListenPort { get; set; } = 47311;

    // Origin przeglądarki dopuszczone do wywołań lokalnego API (patrz etap 2, sekcja 15).
    // Domyślnie produkcyjna domena xkantor.app + localhost (praca developerska/testowa) -
    // port 5080 to domyślny port dev xKantor.APP (patrz Properties/launchSettings.json w
    // E:\kantorApp, zmienione z 5050 na 5080 - zaktualizowane tutaj 2026-09-16, żeby domyślna
    // konfiguracja Agenta od razu działała z aktualnym środowiskiem dev bez ręcznej edycji
    // agent.json).
    public List<string> AllowedOrigins { get; set; } = new()
    {
        "https://xkantor.app",
        "https://www.xkantor.app",
        "http://localhost:5080",
        "https://localhost:5080"
    };

    // Moduły włączone/wyłączone przez administratora stanowiska (np. brak fizycznej
    // drukarki/wyświetlacza na danym stanowisku).
    public Dictionary<string, bool> EnabledModules { get; set; } = new()
    {
        ["Printing"] = true,
        ["Devices"] = true,
        ["Monitors"] = true,
        ["CurrencyDisplay"] = true,
        ["Board"] = true
    };

    public bool JestModulWlaczony(string nazwa) => !EnabledModules.TryGetValue(nazwa, out var wlaczony) || wlaczony;
}
