using System;

namespace Auditorium.Core.Entities
{
    public class Lesson
    {
        public int Id { get; set; }
        public int? Day { get; set; }
        public int? DayNumber { get; set; }
        public string? Time { get; set; }
        public DateTime? TimeFrom { get; set; }
        public DateTime? TimeTo { get; set; }
        public string? Subject { get; set; }
        public string? TeacherFull { get; set; }
        public string? Teacher { get; set; }
        public string? GroupName { get; set; }
        public int? RoomCode { get; set; }
        public string? RoomName { get; set; }
        public int? AudienceId { get; set; }
        public string? Semester { get; set; }
    }
}