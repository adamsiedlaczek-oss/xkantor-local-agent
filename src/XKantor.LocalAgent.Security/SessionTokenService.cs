using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XKantor.LocalAgent.Security;

public sealed record SessionTokenPayload(string StationId, DateTimeOffset IssuedAtUtc, DateTimeOffset ExpiresAtUtc, string[] Scopes);

public enum SessionTokenValidationResult
{
    Valid,
    Invalid,
    Expired,
    MissingScope
}

// Krótkoterminowe, przypisane do stanowiska i ograniczone zakresem tokeny sesji - patrz etap 2,
// sekcja 14 ("Nie używaj jednego stałego sekretu w JavaScript... każde żądanie ograniczone
// czasowo/przypisane do stanowiska/ograniczone zakresem"). Klucz podpisujący (HMAC) jest
// losowany PRZY KAŻDYM STARCIE procesu i istnieje wyłącznie w pamięci - nigdy nie trafia na
// dysk ani do logów - więc token traci ważność również przy restarcie Agenta, nie tylko po
// upływie ExpiresAtUtc. Uzyskanie tokenu wymaga znajomości StationSecret (ustalonego raz przy
// parowaniu - patrz PairingService/IdentityService.ZweryfikujStationSecret), więc sam token
// nigdy nie jest jedynym sekretem, jaki przegląda przechowuje długoterminowo.
[SupportedOSPlatform("windows")]
public sealed class SessionTokenService
{
    private static readonly TimeSpan DomyslnyTtl = TimeSpan.FromMinutes(2);

    private readonly IdentityService _identity;
    private readonly byte[] _kluczPodpisu = RandomNumberGenerator.GetBytes(32);

    public SessionTokenService(IdentityService identity)
    {
        _identity = identity;
    }

    public string? WystawToken(string stationSecretBase64, IReadOnlyCollection<string> scopes, TimeSpan? ttl = null)
    {
        if (!_identity.CzySparowany || _identity.StationId is null) return null;
        if (!_identity.ZweryfikujStationSecret(stationSecretBase64)) return null;

        var teraz = DateTimeOffset.UtcNow;
        var payload = new SessionTokenPayload(_identity.StationId, teraz, teraz + (ttl ?? DomyslnyTtl), scopes.ToArray());
        var payloadJson = JsonSerializer.SerializeToUtf8Bytes(payload);
        var podpis = HMACSHA256.HashData(_kluczPodpisu, payloadJson);

        return $"{Base64Url(payloadJson)}.{Base64Url(podpis)}";
    }

    public (SessionTokenValidationResult Result, SessionTokenPayload? Payload) Waliduj(string? token, string wymaganyZakres)
    {
        if (string.IsNullOrWhiteSpace(token)) return (SessionTokenValidationResult.Invalid, null);

        var czesci = token.Split('.', 2);
        if (czesci.Length != 2) return (SessionTokenValidationResult.Invalid, null);

        byte[] payloadBytes, podanyPodpis;
        try
        {
            payloadBytes = FromBase64Url(czesci[0]);
            podanyPodpis = FromBase64Url(czesci[1]);
        }
        catch (FormatException)
        {
            return (SessionTokenValidationResult.Invalid, null);
        }

        var oczekiwanyPodpis = HMACSHA256.HashData(_kluczPodpisu, payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(oczekiwanyPodpis, podanyPodpis))
        {
            return (SessionTokenValidationResult.Invalid, null);
        }

        SessionTokenPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SessionTokenPayload>(payloadBytes);
        }
        catch (JsonException)
        {
            return (SessionTokenValidationResult.Invalid, null);
        }

        if (payload is null) return (SessionTokenValidationResult.Invalid, null);
        if (!string.Equals(payload.StationId, _identity.StationId, StringComparison.Ordinal))
        {
            return (SessionTokenValidationResult.Invalid, null);
        }
        if (DateTimeOffset.UtcNow >= payload.ExpiresAtUtc)
        {
            return (SessionTokenValidationResult.Expired, payload);
        }
        if (!payload.Scopes.Contains(wymaganyZakres, StringComparer.Ordinal))
        {
            return (SessionTokenValidationResult.MissingScope, payload);
        }

        return (SessionTokenValidationResult.Valid, payload);
    }

    private static string Base64Url(byte[] dane) =>
        Convert.ToBase64String(dane).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string tekst)
    {
        var zPaddingiem = tekst.Replace('-', '+').Replace('_', '/');
        var reszta = zPaddingiem.Length % 4;
        if (reszta > 0) zPaddingiem += new string('=', 4 - reszta);
        return Convert.FromBase64String(zPaddingiem);
    }
}
