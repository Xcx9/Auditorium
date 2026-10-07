using Auditorium.Core.Entities;
using Auditorium.Core.Services;
using Auditorium.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Auditorium.Infrastructure.Services;

public class ScheduleStorage
{
    private readonly AppDbContext _db;

    public ScheduleStorage(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> ReplaceAllAsync(
        List<Lesson> lessons,
        List<Audience> audiences,
        CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // 1. „истим таблицы.
        //    ѕор€док важен: сначала зан€ти€, потом аудитории,
        //    чтобы не срабатывал SetNull/Cascade на удал€емых строках.
        await _db.Lessons.ExecuteDeleteAsync(ct);
        await _db.Audiences.ExecuteDeleteAsync(ct);

        // 2. «агружаем новые данные.
        _db.Audiences.AddRange(audiences);
        _db.Lessons.AddRange(lessons);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return lessons.Count;
    }
}