using Auditorium.Core.DTOs;
using Auditorium.Core.Entities;
using Auditorium.Core.Interfaces;

namespace Auditorium.Core.Services;

public sealed class ScheduleUpdater(IScheduleParser parser, IScheduleSnapshotStore store)
{
    public Task<ScheduleSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        store.GetSnapshotAsync(cancellationToken);

    public async Task<ScheduleRefreshResult> Refresh(
        ScheduleLoadRequest request, CancellationToken cancellationToken = default)
    {
        var candidate = await parser.LoadAndValidate(request, cancellationToken);
        if (candidate is ScheduleLoadResult.Failed failure)
            return await FailAsync(failure.Message, cancellationToken);

        try
        {
            var state = await store.SaveAsync((ScheduleLoadResult.Loaded)candidate, cancellationToken);
            return new ScheduleRefreshResult(state, null);
        }
        catch (ScheduleStorageException error)
        {
            return await FailAsync(error.Message, cancellationToken);
        }
    }

    private async Task<ScheduleRefreshResult> FailAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            var state = await store.RecordFailureAsync(cancellationToken);
            return new ScheduleRefreshResult(state, message);
        }
        catch (ScheduleStorageException error)
        {
            return new ScheduleRefreshResult(null, $"{message} {error.Message}");
        }
    }
}
