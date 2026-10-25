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

    public async Task<Room?> CheckRoomExistance(int roomId)
    {
        return await _context.Rooms
            .FirstOrDefaultAsync(room => room.Id == roomId);
    }
        
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
            .FirstOrDefaultAsync(mark =>
                mark.UserId == userId && mark.ClosedAt == null);
    }
    
    public async Task<OccupancyMark?> GetDueMarksAsync(DateTime now)
    {
        return await _context.OccupancyMarks
            .FirstOrDefaultAsync(mark => mark.ClosedAt <= now);
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