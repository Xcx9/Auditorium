using Auditorium.Core.Entities;
using Auditorium.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Auditorium.Infrastructure.Data
{
    public class RoomRepository : IRoomRepository
    {
        private readonly AppDbContext _context;

        public RoomRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Room>> GetAllAsync()
        {
            return await _context.Rooms
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
