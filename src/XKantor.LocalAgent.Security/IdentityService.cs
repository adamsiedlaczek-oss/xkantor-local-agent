using System.Runtime.Versioning;
using System.Security.Cryptography;
using XKantor.LocalAgent.Core.Models;

namespace XKantor.LocalAgent.Security;

public sealed record PublicKeyInfo(string PublicKeySpkiBase64, string Thumbprint, string Algorithm);

// Zarządza parą kluczy Agenta (ECDSA P-256) - generowanie przy pierwszym uruchomieniu,
// bezpieczne przechowywanie (IdentityStore -> DPAPI), podpisywanie żądań (parowanie,
// odnowienie tożsamości). Patrz etap 2, sekcje 10-14.
//
// Jeden proces = jeden IdentityService w pamięci (Singleton w DI, patrz Service/Program.cs) -
// klucz prywatny jest odszyfrowywany raz przy starcie i trzymany w pamięci jako ECDsa, nigdy
// ponownie zapisywany na dysk poza jawną zmianą (parowanie/odnowienie).
[SupportedOSPlatform("windows")]
public sealed class IdentityService : IDisposable
{
    private const string Algorytm = "ECDSA-P256-SHA256";

    private readonly IdentityStore _store;
    private ECDsa? _klucz;
    private IdentityRecord? _rekord;
    private readonly object _blokada = new();

    public IdentityService(IdentityStore store)
    {
        _store = store;
    }

    public bool CzySparowany => _rekord?.StationId is not null && _rekord?.Certificate is not null;
    public string? StationId => _rekord?.StationId;
    public StationCertificate? Certificate => _rekord?.Certificate;

    // Generuje nową parę kluczy przy pierwszym starcie albo ładuje istniejącą - idempotentne,
    // bezpieczne do wołania przy każdym starcie Agenta (patrz Service/Program.cs).
    public void EnsureKeyPair()
    {
        lock (_blokada)
        {
            if (_klucz is not null) return;

            var istniejacy = _store.Zaladuj();
            if (istniejacy is not null)
            {
                _rekord = istniejacy;
                _klucz = ECDsa.Create();
                _klucz.ImportPkcs8PrivateKey(Convert.FromBase64String(istniejacy.PrivateKeyPkcs8Base64), out _);
                return;
            }

            _klucz = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var prywatny = _klucz.ExportPkcs8PrivateKey();
            var publiczny = _klucz.ExportSubjectPublicKeyInfo();

            _rekord = new IdentityRecord
            {
                PrivateKeyPkcs8Base64 = Convert.ToBase64String(prywatny),
                PublicKeySpkiBase64 = Convert.ToBase64String(publiczny)
            };
            _store.Zapisz(_rekord);
        }
    }

    public PublicKeyInfo GetPublicKeyInfo()
    {
        if (_rekord is null) throw new InvalidOperationException("Tożsamość nie została jeszcze zainicjalizowana - wywołaj EnsureKeyPair().");
        return new PublicKeyInfo(_rekord.PublicKeySpkiBase64, ObliczThumbprint(_rekord.PublicKeySpkiBase64), Algorytm);
    }

    public static string ObliczThumbprint(string publicKeySpkiBase64)
    {
        var bajty = Convert.FromBase64String(publicKeySpkiBase64);
        var hash = SHA256.HashData(bajty);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Podpisuje dowolne dane kluczem prywatnym Agenta (żądania parowania/odnowienia) -
    // ECDSA/SHA256. Weryfikacja po stronie centrali (poza zakresem tego repo, patrz
    // docs/PAIRING.md) używa klucza publicznego zwróconego przez GetPublicKeyInfo().
    public byte[] Sign(ReadOnlySpan<byte> dane)
    {
        if (_klucz is null) throw new InvalidOperationException("Tożsamość nie została jeszcze zainicjalizowana.");
        return _klucz.SignData(dane.ToArray(), HashAlgorithmName.SHA256);
    }

    public void ZapiszSparowanie(string stationId, string stationSecretBase64, StationCertificate certyfikat)
    {
        lock (_blokada)
        {
            if (_rekord is null) throw new InvalidOperationException("Tożsamość nie została jeszcze zainicjalizowana.");

            _rekord.StationId = stationId;
            _rekord.StationSecretBase64 = stationSecretBase64;
            _rekord.Certificate = certyfikat;
            _store.Zapisz(_rekord);
        }
    }

    public void ZapiszOdnowionyCertyfikat(StationCertificate certyfikat)
    {
        lock (_blokada)
        {
            if (_rekord?.StationId is null) throw new InvalidOperationException("Stanowisko nie jest sparowane.");
            _rekord.Certificate = certyfikat;
            _store.Zapisz(_rekord);
        }
    }

    public bool ZweryfikujStationSecret(string podanySekretBase64)
    {
        if (_rekord?.StationSecretBase64 is null) return false;
        var oczekiwany = Convert.FromBase64String(_rekord.StationSecretBase64);
        byte[] podany;
        try { podany = Convert.FromBase64String(podanySekretBase64); }
        catch (FormatException) { return false; }

        return CryptographicOperations.FixedTimeEquals(oczekiwany, podany);
    }

    public IdentityStatus GetIdentityStatus(RenewalPolicy polityka)
    {
        if (!CzySparowany || _rekord?.Certificate is null)
        {
            return new IdentityStatus { CzySparowany = false, CertyfikatWazny = false, WOkresieAwaryjnym = false };
        }

        var cert = _rekord.Certificate;
        var teraz = DateTimeOffset.UtcNow;
        var wazny = teraz < cert.ExpiresAtUtc;
        var wGrace = !wazny && teraz < cert.ExpiresAtUtc + polityka.OkresAwaryjny;

        return new IdentityStatus
        {
            CzySparowany = true,
            StationId = _rekord.StationId,
            CertyfikatWazny = wazny,
            WOkresieAwaryjnym = wGrace,
            WygasaUtc = cert.ExpiresAtUtc,
            DniDoWygasniecia = (int)Math.Ceiling((cert.ExpiresAtUtc - teraz).TotalDays)
        };
    }

    public void Dispose() => _klucz?.Dispose();
}
