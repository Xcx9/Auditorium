
using System;

namespace Auditorium.Core.Entities
{
    public class ScheduleEntry
    {
        // Уникальный идентификатор занятия
        public int Id { get; set; }

        // Идентификатор аудитории
        public int RoomId { get; set; }

        // Время начала занятия
        public DateTime Start { get; set; }

        // Время окончания занятия
        public DateTime End { get; set; }

        // Связь занятия с аудиторией
        public Room Room { get; set; } = null!;

        // Проверяет, идет ли занятие в указанный момент
        public bool IsActiveAt(DateTime moment)
        {
            return moment >= Start && moment < End;
        }

        // Возвращает время начала занятия,
        // если оно еще не наступило
        public DateTime? NextStartAfter(DateTime moment)
        {
            return Start > moment ? Start : null;
        }
    }
}
