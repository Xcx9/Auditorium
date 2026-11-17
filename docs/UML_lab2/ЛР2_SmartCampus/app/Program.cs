using SmartCampus;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("SmartCampus — учебная демонстрация ядра ЛР2");
Console.WriteLine("Контрольные данные, без подключения к сайту и без веб-интерфейса.");
int passed = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

// Управляемые часы нужны только для воспроизводимой демонстрации границ времени.
// Пользователь будущего веб-приложения время создания не задает.
var now = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.FromHours(3));
var date = DateOnly.FromDateTime(now.DateTime);
var snapshot = new ScheduleSnapshot {
    LastSuccessfulUpdate = now, UpdateFailed = false,
    Rooms = new() {
        new Room { Id=1, Number="3101", Building="3", ScheduleDates=new(){date} },
        new Room { Id=2, Number="3102", Building="3", ScheduleDates=new(){date} },
        new Room { Id=3, Number="4201", Building="4", ScheduleDates=new() },
        new Room { Id=4, Number="4202", Building="4", ScheduleDates=new(){date} }
    },
    Lessons = new() {
        new Lesson { Id=1, RoomId=1, StartsAt=now.AddHours(1), EndsAt=now.AddHours(2) }
    }
};
var service = new CampusService(snapshot, () => now);
Check("поиск исключает аудиторию без данных", service.FindFree().Count == 3);
Check("фильтры по корпусу и части номера", service.FindFree("3", "02").Single().Id == 2);
Check("карточка содержит бинарный статус Room", service.GetRoom(1).Status == "свободно");
Check("нет данных — создание запрещено", service.CreateMark(1, 3) == "Нет данных на текущую дату");
Check("создание в свободной аудитории", service.CreateMark(1, 1) == "Отметка создана");
Check("статус после создания занят", service.GetRoom(1).Status == "занято");
Check("одна активная отметка у пользователя", service.CreateMark(1, 2) == "У пользователя уже есть активная отметка");
Check("одна активная отметка у аудитории", service.CreateMark(2, 1) == "Аудитория занята");
var mark = service.GetOwnMark(1);
Check("просмотр собственной отметки", mark.RoomId == 1);
Check("чужую отметку снять нельзя", service.CancelMark(2, mark.Id) == "Нельзя снять чужую отметку");
var oldTime = snapshot.LastSuccessfulUpdate;
service.ApplySchedule(null, false);
Check("сбой сохраняет копию и время обновления", service.Snapshot == snapshot && snapshot.UpdateFailed && snapshot.LastSuccessfulUpdate == oldTime);
// Дополнительно убираем покрытие даты: даже при полном отсутствии расписания снятие работает.
snapshot.Rooms[0].ScheduleDates.Clear();
Check("снятие своей отметки без расписания", service.CancelMark(1, mark.Id) == "Отметка снята");
Check("повторное снятие безопасно", service.CancelMark(1, mark.Id) == "Активной отметки уже нет");
snapshot.Rooms[0].ScheduleDates.Add(date);
Check("после снятия свободно, если занятия нет", service.GetRoom(1).Status == "свободно");
service.CreateMark(1, 1);
now = now.AddHours(1); // Ровно 09:00, начало занятия.
Check("автоматическое снятие на границе начала", service.GetOwnMark(1) == null);
Check("при автоснятии аудитория занята занятием", service.GetRoom(1).Status == "занято");
Check("во время занятия создание запрещено", service.CreateMark(2, 1) == "Аудитория занята");
now = now.AddHours(1); // Ровно 10:00, конец занятия.
Check("на границе конца занятия свободно", service.GetRoom(1).Status == "свободно");
service.CreateMark(1, 2); // У аудитории 2 нет следующего занятия.
now = now.AddMinutes(61);
Check("61 минута не снимает отметку без занятия", service.GetOwnMark(1) != null);
service.CancelMark(1, service.GetOwnMark(1).Id);

// Два конкурирующих запроса. Порядок победителей не важен, важен ровно один успех.
var results = await Task.WhenAll(
    Task.Run(() => service.CreateMark(10, 2)),
    Task.Run(() => service.CreateMark(11, 2)));
Check("два пользователя — один победитель на аудиторию", results.Count(r => r == "Отметка создана") == 1);
foreach (int userId in new[] {10,11}) {
    var active = service.GetOwnMark(userId);
    if (active != null) service.CancelMark(userId, active.Id);
}
results = await Task.WhenAll(
    Task.Run(() => service.CreateMark(12, 1)),
    Task.Run(() => service.CreateMark(12, 4)));
Check("один пользователь — одна отметка при двух запросах", results.Count(r => r == "Отметка создана") == 1);

// Отдельный сервис: запрос приходит уже после окончания следующего занятия.
var later = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.FromHours(3));
var missed = new CampusService(snapshot, () => later);
missed.CreateMark(20, 1);
later = later.AddHours(3);
Check("пропущенное событие начала обрабатывается при запросе", missed.GetOwnMark(20) == null);
Console.WriteLine($"RESULT: {passed} checks passed");
