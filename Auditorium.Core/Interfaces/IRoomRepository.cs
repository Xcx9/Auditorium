using Auditorium.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auditorium.Core.Interfaces
{
    public interface IRoomRepository
    {
        Task<List<Room>> GetAllAsync();
        // При необходимости добавьте другие методы:
        // Task<Room?> GetByIdAsync(int id);
    }
}
