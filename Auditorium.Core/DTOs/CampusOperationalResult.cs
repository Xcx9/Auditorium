namespace Auditorium.Core.DTOs;

public enum CampusOperationCode
    {
        Success,
        RoomNotFound,
        UserNotFound,
        ScheduleNotCovered,
        UserAlreadyHasActiveMark,
        RoomAlreadyOccupied,
        MarkNotFound,
        NotOwner,
        MarkAlreadyClosed,
        ConcurrentConflict
    }

public sealed record CampusOperationResult(CampusOperationCode Code, string Message)
    {
        public bool IsSuccess => Code == CampusOperationCode.Success;
    }