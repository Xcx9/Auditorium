using System.Collections.Generic;

namespace Auditorium.Core.Entities
{
    /// <summary>
    /// Учебная аудитория (например, "1201 м").
    /// </summary>
    public class Audience
    {
        /// <summary>
        /// Числовой код аудитории в системе МИЭТ (например, 120).
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Название аудитории (например, "1201 м").
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Занятия, проходящие в этой аудитории.
        /// </summary>
        public List<Lesson> Lessons { get; set; } = new();
    }
}