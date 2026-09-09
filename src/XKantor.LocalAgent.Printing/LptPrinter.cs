using System.Runtime.Versioning;

namespace XKantor.LocalAgent.Printing;

// Drukowanie bezpośrednio na porcie równoległym (LPT1..LPT4) - etap 2, sekcja 17. Windows
// udostępnia porty równoległe jako zwykłe urządzenia znakowe (\\.\LPT1), więc zwykły
// FileStream/File.Open na tej nazwie wystarcza - bez dodatkowych P/Invoke.
[SupportedOSPlatform("windows")]
public sealed class LptPrinter : IPrinter
{
    public string Nazwa { get; }
    private readonly string _sciezkaUrzadzenia;

    public LptPrinter(string port)
    {
        Nazwa = port;
        _sciezkaUrzadzenia = $@"\\.\{port}";
    }

    public async Task<PrintResult> DrukujAsync(byte[] dane, string nazwaDokumentu, CancellationToken ct = default)
    {
        try
        {
            await using var strumien = new FileStream(_sciezkaUrzadzenia, FileMode.Open, FileAccess.Write, FileShare.None);
            await strumien.WriteAsync(dane, ct);
            await strumien.FlushAsync(ct);
            return new PrintResult(true, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new PrintResult(false, $"Port {Nazwa} niedostępny: {ex.Message}");
        }
    }
}
