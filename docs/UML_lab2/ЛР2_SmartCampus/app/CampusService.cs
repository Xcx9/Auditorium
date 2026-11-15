namespace SmartCampus;

// Учебное ядро ЛР2. Здесь показываем логику диаграмм на контрольных данных.
// В рабочем веб-приложении lock заменяется транзакцией БД и уникальными индексами.
public class CampusService
{
    private readonly object gate = new(); // Один ключ для всех операций над общими данными.
    private readonly Func<DateTimeOffset> clock; // В рабочей системе — серверные часы.
    private readonly List<OccupancyMark> marks = new();
    private int nextId = 1;
    public ScheduleSnapshot Snapshot { get; private set; }

    public CampusService(ScheduleSnapshot snapshot, Func<DateTimeOffset> clock)
    {
        Snapshot = snapshot;
        this.clock = clock;
    }

    // Все внутренние вызовы происходят под одним lock. Повторный lock в C# допустим.
    public void ReleaseDueMarks(DateTimeOffset now)
    {
        lock (gate)
        {
            foreach (var mark in marks.Where(m => m.ClosedAt == null))
            {
                // Берем первое известное занятие после создания, даже если его начало
                // уже пропущено из-за остановки программы. Не ищем только будущие занятия.
                var next = Snapshot.Lessons
                    .Where(l => l.RoomId == mark.RoomId && l.StartsAt > mark.CreatedAt)
                    .OrderBy(l => l.StartsAt).FirstOrDefault();
                if (next != null)
                    mark.AutoReleaseAt = next.StartsAt;
                // Если новой информации нет, сохраняем ранее известную границу.
                // Ошибка загрузки расписания не должна продлевать отметку.
                if (mark.AutoReleaseAt.HasValue && now >= mark.AutoReleaseAt.Value)
                    mark.ClosedAt = mark.AutoReleaseAt.Value;
            }
        }
    }

    private bool HasData(Room room, DateTimeOffset now) =>
        room.ScheduleDates.Contains(DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(3)).DateTime));

    private void UpdateStatus(Room room, DateTimeOffset now)
    {
        // Конец занятия не включаем: в 10:00 занятие 09:00–10:00 уже завершено.
        bool lessonNow = Snapshot.Lessons.Any(l => l.RoomId == room.Id
            && l.StartsAt <= now && now < l.EndsAt);
        bool marked = marks.Any(m => m.RoomId == room.Id && m.ClosedAt == null);
        room.Status = lessonNow || marked ? "занято" : "свободно";
    }

    public List<Room> FindFree(string building = "", string numberPart = "")
    {
        lock (gate)
        {
            var now = clock();
            ReleaseDueMarks(now);
            var availableData = Snapshot.Rooms.Where(r => HasData(r, now)).ToList();
            foreach (var room in availableData) UpdateStatus(room, now);
            return availableData.Where(r => r.Status == "свободно"
                && (building == "" || r.Building == building)
                && r.Number.Contains(numberPart, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    public Room GetRoom(int roomId)
    {
        lock (gate)
        {
            var now = clock();
            ReleaseDueMarks(now);
            var room = Snapshot.Rooms.SingleOrDefault(r => r.Id == roomId);
            // null — нет карточки с достоверным статусом, это не третий статус Room.
            if (room == null || !HasData(room, now)) return null;
            UpdateStatus(room, now);
            return room;
        }
    }

    public OccupancyMark GetOwnMark(int userId)
    {
        lock (gate)
        {
            ReleaseDueMarks(clock());
            return marks.SingleOrDefault(m => m.UserId == userId && m.ClosedAt == null);
        }
    }

    public string CreateMark(int userId, int roomId)
    {
        lock (gate) // Проверка и запись не могут перемешаться с другим запросом.
        {
            var now = clock();
            ReleaseDueMarks(now);
            var room = Snapshot.Rooms.SingleOrDefault(r => r.Id == roomId);
            if (room == null || !HasData(room, now)) return "Нет данных на текущую дату";
            if (marks.Any(m => m.UserId == userId && m.ClosedAt == null))
                return "У пользователя уже есть активная отметка";
            UpdateStatus(room, now);
            if (room.Status == "занято") return "Аудитория занята";

            var next = Snapshot.Lessons.Where(l => l.RoomId == roomId && l.StartsAt > now)
                .OrderBy(l => l.StartsAt).FirstOrDefault();
            marks.Add(new OccupancyMark {
                Id = nextId++, UserId = userId, RoomId = roomId, CreatedAt = now,
                AutoReleaseAt = next?.StartsAt, ClosedAt = null
            });
            UpdateStatus(room, now);
            return "Отметка создана";
        }
    }

    public string CancelMark(int userId, int markId)
    {
        lock (gate)
        {
            var mark = marks.SingleOrDefault(m => m.Id == markId);
            if (mark == null) return "Активной отметки уже нет";
            // Проверяем автора до изменения. Нельзя передать чужой ID и снять отметку.
            if (mark.UserId != userId) return "Нельзя снять чужую отметку";
            if (mark.ClosedAt != null) return "Активной отметки уже нет";
            mark.ClosedAt = clock();
            // НЕТ проверки «расписание доступно» и НЕТ запрета из-за активной отметки.
            return "Отметка снята";
        }
    }

    // Здесь имитируется результат парсера. Это не реализация загрузки сайта МИЭТ.
    public void ApplySchedule(ScheduleSnapshot candidate, bool valid)
    {
        lock (gate)
        {
            if (!valid || candidate == null)
            {
                Snapshot.UpdateFailed = true; // Старые данные и время не меняем.
                return;
            }
            candidate.UpdateFailed = false;
            candidate.LastSuccessfulUpdate = clock();
            Snapshot = candidate;
            ReleaseDueMarks(clock());
        }
    }
}
