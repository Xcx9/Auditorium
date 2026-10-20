using System;
using System.Collections.Generic;
using System.Text;
using Auditorium.Core.Entities;

namespace Auditorium.Core.Services
{
    enum RoomState
    {
        Free,
        Busy
    }

    public enum MarkResult
    {
        Success,
        AlreadyBusyByUser,
        AlreadyBusyByRoom,
        NotOwner,
        ClassInProgress,
        NotFound
    }

    internal class RoomStatusService
    {
        private readonly List<RoomStatus> _roomStates = new();
        public RoomState GetStatus(Room room, DateTime time)
        { return room.State; }
        public void SetStatus(Room room, RoomState state)
        { room.State = state; }
        public List<RoomStatus> GetFreeRooms(DateTime now)
        {
            var result = new List<Room>();

            foreach (var room in _rooms.GetAll())
            {
                if (!room.IsEnabled) continue;   // закрытые аудитории не показываем

                var state = GetStatus(room, now);
                if (state == RoomState.Free)
                    result.Add(new RoomStatus(room, state));
            }

            return result;
        }
        public MarkResult CreateMark(Room room, User user)
        {  }
        public MarkResult CancelMark(OccupancyMark mark, User user)
        {  }
        public OccupancyMark? GetActiveMarkOf(User user)
        { return null; }
        public void AutoReleaseMarksAtClassStart(DateTime now)
        { }
    }
}

