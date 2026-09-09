namespace XKantor.LocalAgent.Api.Dto;

public sealed record PairingStartResponse(string PairingCode, DateTimeOffset ExpiresAtUtc, string PublicKeySpkiBase64, string PublicKeyThumbprint, string Algorithm);

// IssuerToken: blob dostarczony przez xkantor.app za pośrednictwem przeglądarki - patrz
// Security/PairingService.cs i docs/PAIRING.md. Format zostanie doprecyzowany z backendem
// (docs/QUESTIONS.csv #002) - Agent na tym etapie przyjmuje go jako nieprzezroczysty ciąg.
public sealed record PairingCompleteRequest(string PairingCode, string StationId, string IssuerToken);
public sealed record PairingCompleteResponse(string StationId, string StationSecretBase64, DateTimeOffset CertificateExpiresAtUtc);
