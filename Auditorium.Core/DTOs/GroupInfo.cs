namespace Auditorium.Core.DTOs
{
    /// <summary>
    /// Информация об учебной группе.
    /// </summary>
    public class GroupInfo
    {
        // Код группы.
        public string? Code { get; set; }

        // Название группы (например, "ЭН-35").
        public string? Name { get; set; }
    }
}