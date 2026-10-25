using Auditorium.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auditorium.Core.Interfaces
{
        public interface IMarkRepository
        { 
                Task<List<OccupancyMark>> GetByIdAsync(int id);
                // При необходимости добавьте другие методы:
                // Task<OccupancyMark?> GetByIdAsync(int id);
        }
}