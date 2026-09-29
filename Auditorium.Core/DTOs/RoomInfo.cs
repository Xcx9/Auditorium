namespace Auditorium.Core.DTOs
{
    /// <summary>
    /// Информация об аудитории.
    /// </summary>
    public class RoomInfo
    {
        // Уникальный код аудитории.
        public int? Code { get; set; }

        // Название аудитории (например, "3109 м").
        public string? Name { get; set; }
    }
}