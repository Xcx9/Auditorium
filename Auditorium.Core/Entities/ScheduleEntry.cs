using System;
using System.Collections.Generic;
using System.Text;

namespace Auditorium.Core.Entities
{
    internal class ScheduleEntry
    {
        private int Id;
        private int RoomId;
        private DateTime Start;
        private DateTime End;
        public bool IsActiveAt( DateTime moment) 
        { return false; }
        public DateTime? NextStartAfter( DateTime moment ) 
        { return null; }
    }
}
