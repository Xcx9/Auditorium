using Auditorium.Core.Entities;
using Auditorium.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Auditorium.Infrastructure.Data;

public class CampusStore : ICampusStore
{
    private readonly AppDbContext _context;

    public CampusStore(AppDbContext context)
    {
        _context = context;
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
        => _context.ScheduleEntries.AnyAsync(lesson => lesson.RoomId == roomId
            && lesson.StartsAt <= now && now < lesson.EndsAt);

    public Task<DateTimeOffset?> GetNextLessonStartAsync(int roomId, DateTimeOffset now)
        => _context.ScheduleEntries
            .Where(lesson => lesson.RoomId == roomId && lesson.StartsAt > now)
            .OrderBy(lesson => lesson.StartsAt)
            .Select(lesson => (DateTimeOffset?)lesson.StartsAt)
            .FirstOrDefaultAsync();

    public Task<bool> HasActiveMarkForRoomAsync(int roomId)
        => _context.OccupancyMarks.AnyAsync(mark => mark.RoomId == roomId && mark.ClosedAt == null);

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
    
    
     public async Task<List<OccupancyMark>> GetDueMarksAsync(
        DateTimeOffset now)
    {
        return await _context.OccupancyMarks
            .Where(mark => mark.ClosedAt == null
                           && mark.AutoReleaseAt != null
                           && mark.AutoReleaseAt <= now)
            .ToListAsync();
    }

    public async Task AddMark(OccupancyMark mark)
    {
        await _context.OccupancyMarks.AddAsync(mark);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveMarkAsync(OccupancyMark mark)
    {
        _context.OccupancyMarks.Remove(mark);
        await _context.SaveChangesAsync();
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