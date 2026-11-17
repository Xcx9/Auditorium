using Auditorium.Core.Entities;
using Auditorium.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Auditorium.Core.DTOs;

namespace Auditorium.Infrastructure.Data;

public class CampusStore : ICampusStore
{
    private readonly AppDbContext _context;
    private const int SqliteConstraintUnique = 2067;

    public CampusStore(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ScheduleSnapshotState> GetScheduleStateAsync()
    {
        var state = await _context.ScheduleImportStates
            .Select(state => new ScheduleSnapshotState(
                state.LastSuccessfulUpdate,
                state.UpdateFailed))
            .SingleOrDefaultAsync();

        return state ?? new ScheduleSnapshotState(null, false);
    }
    
    // ================ Rooms =====================
    public async Task<List<Room>> GetAllRoomsAsync()
    {
        return await _context.Rooms
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Room?> GetRoomByIdAsync(int id)
    {
        return await _context.Rooms
            .FirstOrDefaultAsync(room => room.Id == id);
    }

    public async Task<Room?> CheckRoomExistance(int? roomId)
    {
        return await _context.Rooms
            .FirstOrDefaultAsync(room => room.Id == roomId);
    }
        
    public Task<bool> HasActiveLessonAsync(int roomId, DateTimeOffset now)
        => ActiveLessonsAt(now).AnyAsync(lesson => lesson.RoomId == roomId);

    public Task<List<int>> GetOccupiedRoomIdsAsync(DateTimeOffset now)
        => ActiveLessonsAt(now).Select(lesson => lesson.RoomId)
            .Union(ActiveMarks.Select(mark => mark.RoomId))
            .ToListAsync();

    private IQueryable<Lesson> ActiveLessonsAt(DateTimeOffset now)
        => _context.ScheduleEntries.Where(lesson => lesson.StartsAt <= now && now < lesson.EndsAt);

    public Task<DateTimeOffset?> GetNextLessonStartAsync(int roomId, DateTimeOffset now)
        => _context.ScheduleEntries
            .Where(lesson => lesson.RoomId == roomId && lesson.StartsAt > now)
            .OrderBy(lesson => lesson.StartsAt)
            .Select(lesson => (DateTimeOffset?)lesson.StartsAt)
            .FirstOrDefaultAsync();

    public Task<bool> HasActiveMarkForRoomAsync(int roomId)
        => ActiveMarks.AnyAsync(mark => mark.RoomId == roomId);

    private IQueryable<OccupancyMark> ActiveMarks
        => _context.OccupancyMarks.Where(mark => mark.ClosedAt == null);

    public async Task SaveChangesAsync() => await _context.SaveChangesAsync();

    // ================ Marks ======================
    public async Task<List<OccupancyMark>> GetAllMarksAsync()
    {
        return await _context.OccupancyMarks
            .AsNoTracking()
            .ToListAsync();
    }
    
    public async Task<OccupancyMark?> GetActiveMarkByIdAsync(int userId)
    {
        return await _context.OccupancyMarks
            .Include(mark => mark.Room)
            .FirstOrDefaultAsync(mark =>
                mark.UserId == userId && mark.ClosedAt == null);
    }
    
    
    public Task<int> CloseDueMarksAsync(DateTimeOffset now)
    {
        return _context.OccupancyMarks
            .Where(mark => mark.ClosedAt == null
                           && mark.AutoReleaseAt != null
                           && mark.AutoReleaseAt <= now)
            .ExecuteUpdateAsync(update => update
                .SetProperty(mark => mark.ClosedAt, mark => mark.AutoReleaseAt));
    }

    public async Task<bool> AddMark(OccupancyMark mark)
    {
        _context.OccupancyMarks.Add(mark);
        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is SqliteException sqlite &&
            sqlite.SqliteExtendedErrorCode == SqliteConstraintUnique)
        {
            _context.Entry(mark).State = EntityState.Detached;
            return false;
        }
    }

    /*public async Task RemoveMarkAsync(OccupancyMark mark)
    {
        _context.OccupancyMarks.Remove(mark);
        await _context.SaveChangesAsync();
    }*/
    
    public async Task<bool> RemoveMarkAsync(
        int userId, int markId, DateTimeOffset closedAt)
    {
        var updatedRows = await _context.OccupancyMarks
            .Where(mark => mark.Id == markId
                           && mark.UserId == userId
                           && mark.ClosedAt == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(mark => mark.ClosedAt, (DateTimeOffset?)closedAt));

        return updatedRows == 1;
    }
    
    public async Task<OccupancyMark?> CheckMarkExistance(int markId)
    {
        return await _context.OccupancyMarks
            .FirstOrDefaultAsync(mark => mark.Id == markId);
    }
        
    // ================= Users =======================
    public async Task<User?> CheckUserExistance(int userId)
    {
        return await _context.Users
            .FirstOrDefaultAsync(user => user.Id == userId);
    }
}
