using Auditorium.Core.DTOs;

namespace Auditorium.Core.Interfaces;

public interface IScheduleParser
{
    Task<ScheduleLoadResult> LoadAndValidate(ScheduleLoadRequest request, CancellationToken cancellationToken = default);
}
