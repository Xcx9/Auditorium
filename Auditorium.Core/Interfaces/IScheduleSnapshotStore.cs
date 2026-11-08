using Auditorium.Core.DTOs;
using Auditorium.Core.Entities;

namespace Auditorium.Core.Interfaces;

public interface IScheduleSnapshotStore
{
    Task<ScheduleSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<ScheduleSnapshotState> SaveAsync(ScheduleLoadResult.Loaded candidate, CancellationToken cancellationToken = default);
    Task<ScheduleSnapshotState> RecordFailureAsync(CancellationToken cancellationToken = default);
}

public sealed class ScheduleStorageException(string message, Exception innerException)
    : Exception(message, innerException);
