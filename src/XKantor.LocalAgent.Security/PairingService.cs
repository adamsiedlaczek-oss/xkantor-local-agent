using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace XKantor.LocalAgent.Security;

public sealed record PairingStartResult(string PairingCode, DateTimeOffset ExpiresAtUtc, PublicKeyInfo PublicKey);
public sealed record PairingCompleteResult(string StationSecretBase64, StationCertificate Certificate);

// Parowanie stanowiska - patrz etap 2, sekcja 11 (STATION ID) i docs/PAIRING.md dla pełnego
// przepływu. Kod parowania jest jednorazowy i krótkoterminowy (10 minut), NIGDY nie jest
// stałym hasłem (etap 2, sekcja 11: "Kod... nie może być stałym hasłem").
//
// Agent nie łączy się z Internetem (patrz etap 2, sekcja 1 - architektura) - to przeglądarka
// (już zalogowana do xkantor.app) odczytuje PairingCode+PublicKey z lokalnego API
// (POST /api/v1/pairing/start), przekazuje je do xkantor.app (poza zakresem tego repo),
// dostaje z powrotem StationId + IssuerToken (opaque, format do ustalenia z backendem - patrz
// docs/QUESTIONS.csv #002) i odsyła je z powrotem do Agenta (POST /api/v1/pairing/complete).
[SupportedOSPlatform("windows")]
public sealed class PairingService
{
    private static readonly TimeSpan WaznoscKodu = TimeSpan.FromMinutes(10);
    private const string DozwoloneZnaki = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // bez znaków mylących się (0/O, 1/I/L)

    private readonly IdentityService _identity;
    private readonly RenewalPolicy _polityka;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _oczekujaceKody = new();

    public PairingService(IdentityService identity, RenewalPolicy polityka)
    {
        _identity = identity;
        _polityka = polityka;
    }

    public PairingStartResult StartPairing()
    {
        _identity.EnsureKeyPair();

        var kod = WygenerujKod();
        var wygasa = DateTimeOffset.UtcNow + WaznoscKodu;
        _oczekujaceKody[kod] = wygasa;

        WyczyscWygasleKody();

        return new PairingStartResult(kod, wygasa, _identity.GetPublicKeyInfo());
    }

    public bool ZweryfikujIZuzyjKod(string pairingCode)
    {
        if (!_oczekujaceKody.TryRemove(pairingCode, out var wygasa)) return false;
        return DateTimeOffset.UtcNow < wygasa;
    }

    // Wołane wyłącznie po ZweryfikujIZuzyjKod (patrz Api/Endpoints/PairingEndpoints.cs) -
    // zapisuje sparowanie i generuje nowy StationSecret (patrz IdentityModels.cs -
    // IdentityRecord.StationSecretBase64), zwracany JEDNORAZOWO w odpowiedzi.
    public PairingCompleteResult CompletePairing(string stationId, string issuerToken, TimeSpan? waznosc = null)
    {
        var teraz = DateTimeOffset.UtcNow;
        var certyfikat = new StationCertificate
        {
            StationId = stationId,
            PublicKeyThumbprint = _identity.GetPublicKeyInfo().Thumbprint,
            IssuedAtUtc = teraz,
            ExpiresAtUtc = teraz + (waznosc ?? _polityka.WaznoscCertyfikatu),
            IssuerToken = issuerToken
        };

        var sekret = RandomNumberGenerator.GetBytes(32);
        var sekretBase64 = Convert.ToBase64String(sekret);

        _identity.ZapiszSparowanie(stationId, sekretBase64, certyfikat);

        return new PairingCompleteResult(sekretBase64, certyfikat);
    }

    private static string WygenerujKod()
    {
        Span<char> znaki = stackalloc char[8];
        for (var i = 0; i < znaki.Length; i++)
        {
            znaki[i] = DozwoloneZnaki[RandomNumberGenerator.GetInt32(DozwoloneZnaki.Length)];
        }
        return new string(znaki);
    }

    private void WyczyscWygasleKody()
    {
        var teraz = DateTimeOffset.UtcNow;
        foreach (var (kod, wygasa) in _oczekujaceKody)
        {
            if (teraz >= wygasa) _oczekujaceKody.TryRemove(kod, out _);
        }
    }
}
