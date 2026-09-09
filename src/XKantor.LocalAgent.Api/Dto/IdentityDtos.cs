namespace XKantor.LocalAgent.Api.Dto;

public sealed record RenewalRequestResponse(string PayloadJson, string SignatureBase64);

// Certyfikat odnowiony przez xkantor.app, dostarczony z powrotem przez przeglądarkę - patrz
// Security/RenewalService.cs.
public sealed record InstallCertificateRequest(string StationId, string PublicKeyThumbprint, DateTimeOffset IssuedAtUtc, DateTimeOffset ExpiresAtUtc, string IssuerToken);
