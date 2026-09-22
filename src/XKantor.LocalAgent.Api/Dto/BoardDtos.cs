namespace XKantor.LocalAgent.Api.Dto;

// Jeden wpis = jeden monitor (stanowisko może mieć kilka naraz, patrz BoardService.cs) -
// MonitorStableId jest WYMAGANY (to klucz upsertu), inaczej niż w starszej, jedno-monitorowej
// wersji tego DTO, gdzie mógł być null przy Enabled=false.
public sealed record UpdateBoardConfigRequest(bool Enabled, string MonitorStableId, string? MonitorLabel, string? BoardUrl);

public sealed record BoardConfigEntryResponse(string MonitorStableId, string? MonitorLabel, string? BoardUrl, DateTimeOffset? ConfiguredAtUtc);
