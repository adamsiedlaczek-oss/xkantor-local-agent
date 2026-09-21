namespace XKantor.LocalAgent.Api.Dto;

public sealed record UpdateBoardConfigRequest(bool Enabled, string? MonitorStableId, string? MonitorLabel, string? BoardUrl);

public sealed record BoardConfigResponse(bool Enabled, string? MonitorStableId, string? MonitorLabel, string? BoardUrl, DateTimeOffset? ConfiguredAtUtc);
