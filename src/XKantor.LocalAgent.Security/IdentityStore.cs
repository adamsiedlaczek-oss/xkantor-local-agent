using System.Runtime.Versioning;
using System.Text.Json;
using XKantor.LocalAgent.Core.Configuration;

namespace XKantor.LocalAgent.Security;

// Trwały magazyn IdentityRecord - JSON zaszyfrowany DPAPI (patrz SecureKeyStore.cs). Ścieżka
// pliku przyjmowana w konstruktorze (domyślnie ConfigPaths.IdentitySecureFile - patrz
// rejestracja w Api/ApiExtensions.cs) zamiast na sztywno wewnątrz klasy, żeby testy
// (tests/.../SecurityTests) mogły użyć tymczasowego katalogu zamiast prawdziwego
// %ProgramData%\XKantorLocalAgent na maszynie, na której akurat działają.
[SupportedOSPlatform("windows")]
public sealed class IdentityStore
{
    private static readonly JsonSerializerOptions JsonOpcje = new() { WriteIndented = false };

    private readonly string _sciezkaPliku;

    public IdentityStore() : this(ConfigPaths.IdentitySecureFile) { }

    public IdentityStore(string sciezkaPliku)
    {
        _sciezkaPliku = sciezkaPliku;
    }

    public bool Istnieje() => File.Exists(_sciezkaPliku);

    public IdentityRecord? Zaladuj()
    {
        if (!Istnieje()) return null;

        var zaszyfrowane = File.ReadAllBytes(_sciezkaPliku);
        var json = SecureKeyStore.Unprotect(zaszyfrowane);
        try
        {
            return JsonSerializer.Deserialize<IdentityRecord>(json);
        }
        finally
        {
            Array.Clear(json);
        }
    }

    public void Zapisz(IdentityRecord rekord)
    {
        var katalog = Path.GetDirectoryName(_sciezkaPliku);
        if (!string.IsNullOrEmpty(katalog)) Directory.CreateDirectory(katalog);

        var json = JsonSerializer.SerializeToUtf8Bytes(rekord, JsonOpcje);
        try
        {
            var zaszyfrowane = SecureKeyStore.Protect(json);
            File.WriteAllBytes(_sciezkaPliku, zaszyfrowane);
        }
        finally
        {
            Array.Clear(json);
        }
    }
}
