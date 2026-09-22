namespace XKantor.LocalAgent.Core.Configuration;

// Wszystkie ścieżki danych Agenta w jednym miejscu - patrz docs/DECISIONS.md, decyzja
// "Dane w %ProgramData%, nie w folderze instalacyjnym". %ProgramData% (a nie katalog
// instalacyjny Program Files) - żeby reinstalacja/aktualizacja przez installer (etap 2,
// sekcja 26) nigdy nie nadpisała/usunęła tożsamości (klucz prywatny, certyfikat) ani logów.
// Katalog jest wspólny dla wszystkich użytkowników maszyny (LocalMachine), spójnie z tym, że
// klucz prywatny jest chroniony DPAPI w trybie LocalMachine (patrz Security/SecureKeyStore.cs) -
// usługa Windows zwykle działa na koncie maszynowym (LocalSystem/NetworkService) bez
// załadowanego profilu użytkownika, więc DPAPI w trybie CurrentUser by tu nie zadziałało.
public static class ConfigPaths
{
    public static string RootDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "XKantorLocalAgent");

    public static string ConfigDir => Path.Combine(RootDir, "config");
    public static string SecureDir => Path.Combine(RootDir, "secure");
    public static string LogsDir => Path.Combine(RootDir, "logs");

    // Jedyny folder, w ktorym Agent zapisuje pliki na zadanie SAVE_FILE (xkantor.app, "Zapisz do
    // pliku zamiast drukowac") - celowo JEDEN, ustalony tu, nie dowolna sciezka od wywolujacego
    // (etap 2, sekcja 7: "nigdy polecenie/sciezka od klienta"). Patrz Api/Endpoints/SaveFileEndpoints.cs.
    public static string FilesDir => Path.Combine(RootDir, "files");

    public static string AgentConfigFile => Path.Combine(ConfigDir, "agent.json");
    public static string IdentitySecureFile => Path.Combine(SecureDir, "identity.bin");

    // Konfiguracja tablicy kursów (kiosk na drugim monitorze) - patrz XKantor.LocalAgent.Board.
    // Jedyny writer to Service (przez POST /api/v1/board/config); UserSession tylko czyta,
    // przez named pipe "XKantorLocalAgent.Board", nigdy bezpośrednio z dysku (patrz
    // Board/Ipc/BoardConfigPipeServer.cs) - spójne z tym, że cała reszta trwałej konfiguracji
    // też ma jednego właściciela-pisarza.
    public static string BoardConfigFile => Path.Combine(ConfigDir, "board.json");

    // Konfiguracja fizycznego wyświetlacza kursów (SERIAL_LINE/WYSW8_PEZET) - patrz
    // XKantor.LocalAgent.CurrencyDisplay.CurrencyDisplayConfigStore. Ten sam wzorzec co
    // board.json wyżej - jeden właściciel-pisarz (Service, przez POST /api/v1/currency-display/config).
    public static string CurrencyDisplayConfigFile => Path.Combine(ConfigDir, "currency-display.json");

    // Osobny, trwały profil przeglądarki dla kiosku tablicy - odizolowany od normalnego profilu
    // operatora (Edge), żeby nie mieszać historii/sesji kasjera z oknem klienta, a jednocześnie
    // NIE incognito (trwały cache pomaga przy chwilowej utracie Internetu - patrz zadanie,
    // sekcja 17).
    public static string BoardProfileDir => Path.Combine(RootDir, "board-profile");

    public static void UpewnijSieZeFolderyIstnieja()
    {
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(SecureDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(FilesDir);
        Directory.CreateDirectory(BoardProfileDir);
    }
}
