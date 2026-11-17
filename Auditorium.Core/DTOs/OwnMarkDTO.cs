namespace Auditorium.Core.DTOs;

public sealed record OwnMarkDto(
    int MarkId,
    int RoomId,
    string RoomNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AutoReleaseAt);