using Auditorium.Core.Entities;
using Auditorium.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Auditorium.Infrastructure.Data
{
    public class OccupancyMarkRepository : IMarkRepository
    {
        private readonly AppDbContext _context;

        public OccupancyMarkRepository(AppDbContext context)
        {
            _context = context;
        }

        // Сделать по id, не нужно выводтиь все
        public async Task<List<OccupancyMark>> GetByIdAsync(int id)
        {
            return await _context.OccupancyMarks
                .AsNoTracking()
                .ToListAsync();
        }
    }
}