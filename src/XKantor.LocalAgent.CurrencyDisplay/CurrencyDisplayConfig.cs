namespace XKantor.LocalAgent.CurrencyDisplay;

// Konfiguracja urządzenia wyświetlacza kursów tego stanowiska - trwała (patrz
// CurrencyDisplayConfigStore, %ProgramData%\XKantorLocalAgent\config\currency-display.json),
// ustawiana zdalnie przez xKantor.APP (Moje Stanowisko -> Wyświetlacz PEZET, przez przeglądarkę,
// ten sam wzorzec co board.json - patrz Api/Endpoints/CurrencyDisplayEndpoints.cs).
public sealed class CurrencyDisplayConfig
{
    // "NONE" (domyślnie - NotConfiguredAdapter), "SERIAL_LINE" (SerialLineAdapter) albo
    // "WYSW8_PEZET" (Wysw8PezetAdapter - fizyczna tablica PEZET Opole, zadanie
    // pezet\realizacja.txt).
    public string AdapterType { get; set; } = "NONE";

    // SERIAL_LINE: nazwa portu Windows (np. "COM4"). WYSW8_PEZET: surowy token wysw8.exe -
    // "0".."9" (COM1..COM10, n = COM(n+1)) albo dosłownie "USB" (zadanie sekcja 12 - "nie
    // hardkoduj 3, sprawdź istniejącą konfigurację Agenta i pozwól wybrać COM1/COM2/.../USB").
    public string? Port { get; set; }
    public int BaudRate { get; set; } = 9600;
}
