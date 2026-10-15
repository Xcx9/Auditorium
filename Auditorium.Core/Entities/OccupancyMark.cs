using System;
using System.Collections.Generic;
using System.Text;

namespace Auditorium.Core.Entities
{
    internal class OccupancyMark
    {
        private int Id;
        private int RoomId;
        private int UserId;
        private DateTime CreatedAt;
        public bool IsActive(int userId) 
        { return false; }
        public void Cancel(int userId) 
        { }
    }
}
