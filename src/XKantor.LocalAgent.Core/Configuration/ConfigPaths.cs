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

    public static string AgentConfigFile => Path.Combine(ConfigDir, "agent.json");
    public static string IdentitySecureFile => Path.Combine(SecureDir, "identity.bin");

    public static void UpewnijSieZeFolderyIstnieja()
    {
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(SecureDir);
        Directory.CreateDirectory(LogsDir);
    }
}
