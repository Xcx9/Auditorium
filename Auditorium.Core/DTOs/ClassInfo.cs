namespace Auditorium.Core.DTOs
{
    public class ClassInfo
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? TeacherFull { get; set; }
        public string? Teacher { get; set; }
        public string? Form { get; set; }   // в JSON приходит строка ("Нет", "Лек", "Пр" и т.п.)
    }
}