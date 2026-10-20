using Auditorium.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auditorium.Core.Entities
{
    internal class Room
    {
        private int Id;
        private string Number;
        public RoomState State;
        private int m;
        public int BuildingNumber(int buildingNumber)
        { return buildingNumber; }
        public int FloorNumber(int floorNumber)
        { return floorNumber; }
        public static bool IsValidNumber(int number)
        { return false; }
    }

}
