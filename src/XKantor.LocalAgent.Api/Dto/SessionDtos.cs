namespace XKantor.LocalAgent.Api.Dto;

// Wystawienie krótkoterminowego tokenu sesji - patrz Security/SessionTokenService.cs i etap 2,
// sekcja 14. StationSecretBase64 jest znany przeglądarce wyłącznie dzięki wcześniejszemu
// zakończonemu parowaniu (patrz PairingDtos.cs - PairingCompleteResponse).
public sealed record SessionStartRequest(string StationSecretBase64, string[] Scopes);
public sealed record SessionStartResponse(string Token, DateTimeOffset ExpiresAtUtc);
