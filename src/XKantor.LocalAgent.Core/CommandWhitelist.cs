namespace XKantor.LocalAgent.Core;

// Rejestr JAWNIE dozwolonych operacji lokalnego API - patrz etap 2, sekcja 7 ("NIGDY NIE
// IMPLEMENTUJ executeCommand()/runCMD()/runPowerShell()") i sekcja 8 (COMMAND WHITELIST).
// Api/Endpoints/* mapuje TYLKO te komendy na konkretne, typowane endpointy - nic więcej nie
// jest osiągalne z przeglądarki. Testy (patrz tests/.../CommandWhitelistTests.cs) pilnują, że
// ten zbiór się nie rozrasta bez świadomej decyzji i że każda pozycja ma opis.
public static class CommandWhitelist
{
    public static readonly IReadOnlyDictionary<string, string> Komendy = new Dictionary<string, string>
    {
        ["GET_AGENT_STATUS"] = "Pełny status Agenta i wszystkich modułów (GET /api/v1/status).",
        ["GET_DEVICE_STATUS"] = "Status wykrytych urządzeń/portów (GET /api/v1/devices).",
        ["GET_MONITORS"] = "Lista wykrytych monitorów (GET /api/v1/monitors).",
        ["GET_PRINTERS"] = "Lista wykrytych drukarek (GET /api/v1/printers).",
        ["PRINT_TRANSACTION"] = "Wydruk paragonu/potwierdzenia transakcji (POST /api/v1/print/transaction).",
        ["PRINT_DOCUMENT"] = "Wydruk dokumentu ogólnego (POST /api/v1/print/document).",
        ["SAVE_FILE"] = "Zapis gotowej treści (np. kopia paragonu/raportu do kontroli) do pliku w jedynym, ustalonym folderze na tym stanowisku (POST /api/v1/files/save).",
        ["UPDATE_CURRENCY_DISPLAY"] = "Aktualizacja lokalnego wyświetlacza kursów (POST /api/v1/currency-display/update).",
        ["GET_BOARD_CONFIG"] = "Odczyt konfiguracji tablicy kursów na drugim monitorze (GET /api/v1/board/config).",
        ["UPDATE_BOARD_CONFIG"] = "Zapis konfiguracji tablicy kursów na drugim monitorze (POST /api/v1/board/config).",
    };

    public static bool JestDozwolona(string komenda) => Komendy.ContainsKey(komenda);
}
