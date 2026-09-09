using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XKantor.LocalAgent.Security;

public sealed record RenewalRequestResult(string PayloadJson, string SignatureBase64);

// Odnawianie tożsamości - patrz etap 2, sekcja 12 ("Przed wygaśnięciem Agent powinien
// automatycznie próbować odnowić swoje poświadczenie") i docs/DECISIONS.md ("Agent nie łączy
// się z Internetem - odnowienie przez przeglądarkę jako przekaźnik").
//
// Agent NIE inicjuje połączeń wychodzących (patrz etap 2, sekcja 1/9) - "automatyczne
// odnowienie" oznacza tutaj: Agent śledzi okno odnowienia (RenewalPolicy) i - gdy przeglądarka
// zapyta o status/renewal-request - przygotowuje podpisany dowód posiadania klucza prywatnego.
// Przeglądarka przekazuje ten dowód do xkantor.app (poza zakresem tego repo) i odsyła z
// powrotem nowy certyfikat, który Agent instaluje przez InstallRenewedCertificate.
[SupportedOSPlatform("windows")]
public sealed class RenewalService
{
    private readonly IdentityService _identity;
    private readonly RenewalPolicy _polityka;

    public RenewalService(IdentityService identity, RenewalPolicy polityka)
    {
        _identity = identity;
        _polityka = polityka;
    }

    public bool WymagaOdnowienia()
    {
        var cert = _identity.Certificate;
        if (cert is null) return false;
        return _polityka.WymagaOdnowienia(cert.ExpiresAtUtc);
    }

    // Buduje żądanie odnowienia podpisane kluczem prywatnym Agenta - zawiera nonce (obrona
    // przed replay) i znacznik czasu. Weryfikacja podpisu po stronie centrali (poza zakresem
    // tego repo) potwierdza, że żądanie pochodzi od tego samego Agenta, który był parowany
    // (centrala zna PublicKeyThumbprint z momentu parowania).
    public RenewalRequestResult CreateRenewalRequest()
    {
        if (!_identity.CzySparowany || _identity.Certificate is null)
        {
            throw new InvalidOperationException("Stanowisko nie jest sparowane - brak czego odnawiać.");
        }

        var payload = new
        {
            StationId = _identity.StationId,
            PublicKeyThumbprint = _identity.GetPublicKeyInfo().Thumbprint,
            CurrentExpiresAtUtc = _identity.Certificate.ExpiresAtUtc,
            RequestedAtUtc = DateTimeOffset.UtcNow,
            Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
        };

        var json = JsonSerializer.Serialize(payload);
        var podpis = _identity.Sign(Encoding.UTF8.GetBytes(json));

        return new RenewalRequestResult(json, Convert.ToBase64String(podpis));
    }

    // Instaluje nowy certyfikat dostarczony przez przeglądarkę - waliduje, że dotyczy TEGO
    // samego stanowiska i TEGO samego klucza publicznego (Agent nie przyjmuje "cudzego" certu).
    public void InstallRenewedCertificate(StationCertificate nowyCertyfikat)
    {
        if (_identity.StationId is null)
        {
            throw new InvalidOperationException("Stanowisko nie jest sparowane.");
        }
        if (!string.Equals(nowyCertyfikat.StationId, _identity.StationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Certyfikat dotyczy innego StationId.");
        }
        if (!string.Equals(nowyCertyfikat.PublicKeyThumbprint, _identity.GetPublicKeyInfo().Thumbprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Certyfikat nie pasuje do klucza publicznego tego Agenta.");
        }

        _identity.ZapiszOdnowionyCertyfikat(nowyCertyfikat);
    }
}
