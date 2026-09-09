namespace XKantor.LocalAgent.Security;

// Lekki, własny format "certyfikatu" (JSON podpisany asymetrycznie) zamiast pełnego X.509 -
// patrz docs/DECISIONS.md, decyzja "Lekki podpisany JSON zamiast X.509/PKI". IssuerToken to
// nieprzezroczysty blob dostarczony przez xkantor.app (przez przeglądarkę - Agent nigdy nie
// łączy się z Internetem bezpośrednio, patrz etap 2 sekcja 1 i docs/PAIRING.md) - jego DOKŁADNY
// format podpisu centrali zostanie ustalony przy integracji z backendem xkantor.app, patrz
// docs/QUESTIONS.csv #002. Agent na tym etapie odpowiada za WŁASNĄ parę kluczy, podpisywanie
// żądań i lokalne pilnowanie ważności/grace period.
public sealed class StationCertificate
{
    public required string StationId { get; init; }
    public required string PublicKeyThumbprint { get; init; }
    public required DateTimeOffset IssuedAtUtc { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public required string IssuerToken { get; init; }
}

// Rekord trwały (zaszyfrowany DPAPI na dysku - patrz IdentityStore.cs). Nigdy nie loguj tej
// klasy w całości (patrz etap 2, sekcja 22 - "Nie loguj kluczy prywatnych/sekretów").
public sealed class IdentityRecord
{
    public required string PrivateKeyPkcs8Base64 { get; init; }
    public required string PublicKeySpkiBase64 { get; init; }
    public string? StationId { get; set; }

    // Sekret ustalony przy parowaniu (patrz PairingService.CompletePairingAsync) - przeglądarka
    // dostaje go RAZ, w odpowiedzi na dokończenie parowania, i musi go przedstawić przy każdym
    // żądaniu o nowy krótkoterminowy token sesji (patrz SessionTokenService). Odpowiednik
    // "API key" stanowiska - ale krótkotrwałe tokeny z niego wystawiane, nigdy on sam, są
    // używane do autoryzacji whitelisted komend (etap 2, sekcja 14).
    public string? StationSecretBase64 { get; set; }

    public StationCertificate? Certificate { get; set; }
}
