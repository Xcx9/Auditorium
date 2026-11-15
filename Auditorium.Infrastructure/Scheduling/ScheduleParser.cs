using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Auditorium.Core.DTOs;
using Auditorium.Core.Entities;
using Auditorium.Core.Interfaces;
using Auditorium.Infrastructure.Scheduling.SourceDtos;
using Auditorium.Infrastructure.Time;

namespace Auditorium.Infrastructure.Scheduling;

public sealed class ScheduleParser(HttpClient client) : IScheduleParser
{
    private static readonly Uri SchedulePage = new("https://miet.ru/schedule/");

    public async Task<ScheduleLoadResult> LoadAndValidate(
        ScheduleLoadRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Semester) ||
            request.Through.DayNumber - request.From.DayNumber is < 0 or >= 28)
        {
            return new ScheduleLoadResult.Failed(ScheduleLoadError.InvalidPeriod,
                "Укажите семестр и подтверждённый календарём период до 28 дней включительно.");
        }

        try
        {
            var page = await client.GetStringAsync(SchedulePage, cancellationToken);
            var semesterStart = ReadSemesterStart(page);
            if (request.From < semesterStart)
            {
                return new ScheduleLoadResult.Failed(ScheduleLoadError.InvalidPeriod,
                    "Запрошенный период начинается раньше даты цикла, указанной источником.");
            }

            var dates = Enumerable.Range(request.From.DayNumber, request.Through.DayNumber - request.From.DayNumber + 1)
                .Select(DateOnly.FromDayNumber).ToArray();
            var catalog = await Post<List<RoomInfo?>>("audiences", new ByteArrayContent([]), cancellationToken);
            ValidateCatalog(catalog);

            var campusRooms = catalog.Select(info => info!)
                .Where(info => info.Name!.Trim()[0] is '3' or '4').ToArray();
            if (campusRooms.Length == 0)
                throw new InvalidDataException("В каталоге МИЭТ нет аудиторий 3-го и 4-го корпусов.");

            var rooms = new List<RoomSchedule>(campusRooms.Length);
            foreach (var info in campusRooms)
            {
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(info.Code.ToString(CultureInfo.InvariantCulture)), "audience");
                var data = await Post<ScheduleResponse>("data", form, cancellationToken);
                rooms.Add(CreateRoomSchedule(info, data, request.Semester.Trim(), semesterStart, dates));
                if (rooms.Count < campusRooms.Length)
                    await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
            }

            return new ScheduleLoadResult.Loaded(rooms.AsReadOnly());
        }
        catch (HttpRequestException ex)
        {
            return new ScheduleLoadResult.Failed(ScheduleLoadError.SourceUnavailable,
                ex.StatusCode is { } status ? $"МИЭТ вернул HTTP {(int)status}." : "Источник расписания недоступен.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ScheduleLoadResult.Failed(ScheduleLoadError.SourceUnavailable, "Истекло время ожидания ответа МИЭТ.");
        }
        catch (JsonException)
        {
            return new ScheduleLoadResult.Failed(ScheduleLoadError.InvalidResponse, "МИЭТ вернул некорректный JSON расписания.");
        }
        catch (InvalidDataException ex)
        {
            return new ScheduleLoadResult.Failed(ScheduleLoadError.InvalidResponse, ex.Message);
        }
    }

    private async Task<T> Post<T>(string path, HttpContent content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(SchedulePage, path));
        request.Headers.Referrer = SchedulePage;
        request.Headers.Accept.ParseAdd("application/json");
        request.Content = content;
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidDataException("Источник не подтвердил полный ответ расписания.");

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(body, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Источник вернул пустой ответ.");
    }

    private static DateOnly ReadSemesterStart(string page)
    {
        var match = Regex.Match(page, """\bvar\s+semestr_start\s*=\s*["']([0-9]{4}-[0-9]{2}-[0-9]{2})["']""");
        return DateOnly.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : throw new InvalidDataException("На странице МИЭТ не найдена дата начала цикла расписания.");
    }

    private static void ValidateCatalog(List<RoomInfo?> catalog)
    {
        var codes = new HashSet<int>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (catalog.Count == 0)
            throw new InvalidDataException("МИЭТ вернул пустой каталог аудиторий.");

        foreach (var info in catalog)
        {
            if (info?.Name?.Trim() is not { Length: > 0 and <= 20 } name ||
                info.Code <= 0 || !codes.Add(info.Code) || !names.Add(name))
                throw new InvalidDataException("Каталог МИЭТ содержит неполные или повторяющиеся аудитории.");
        }
    }

    private static RoomSchedule CreateRoomSchedule(
        RoomInfo info, ScheduleResponse data, string semester, DateOnly semesterStart, DateOnly[] dates)
    {
        if (data.Semestr?.Trim() != semester || data.Times is not { Count: > 0 } || data.Data is null)
            throw new InvalidDataException($"Ответ для аудитории {info.Code} неполный или относится к другому семестру.");

        var slots = new HashSet<int>();
        foreach (var slot in data.Times)
        {
            if (slot is null || !TryReadInterval(slot, out _, out _) || !slots.Add(slot.Code))
                throw new InvalidDataException($"Некорректные интервалы звонков для аудитории {info.Code}.");
        }

        var intervals = new HashSet<(int Day, int Week, TimeOnly Start, TimeOnly End)>();
        foreach (var row in data.Data)
        {
            if (row?.Day is not (>= 0 and <= 6) || row.DayNumber is not (>= 0 and <= 3) ||
                row.Room?.Code != info.Code || row.Time is null || !slots.Contains(row.Time.Code) ||
                !TryReadInterval(row.Time, out var start, out var end))
                throw new InvalidDataException($"Некорректное занятие для аудитории {info.Code}.");

            intervals.Add((row.Day.Value, row.DayNumber.Value, start, end));
        }

        var room = new Room(info.Name!.Trim());
        room.ScheduleDates.AddRange(dates);
        var byCycleDay = intervals.ToLookup(x => (x.Day, x.Week));

        // Страница МИЭТ сравнивает начала недель и использует остаток от деления на четыре.
        var daysSinceMonday = ((int)semesterStart.DayOfWeek + 6) % 7;
        var cycleMonday = semesterStart.DayNumber - daysSinceMonday;
        var lessons = new List<Lesson>();
        foreach (var date in dates)
        {
            var week = (date.DayNumber - cycleMonday) / 7 % 4;
            foreach (var interval in byCycleDay[((int)date.DayOfWeek, week)].OrderBy(x => x.Start).ThenBy(x => x.End))
                lessons.Add(new Lesson(room, MoscowClock.AtLocalTime(date, interval.Start),
                    MoscowClock.AtLocalTime(date, interval.End)));
        }

        return new RoomSchedule(info.Code, room, lessons.AsReadOnly());
    }

    private static bool TryReadInterval(TimeSlot slot, out TimeOnly start, out TimeOnly end)
    {
        start = end = default;
        if (slot.Code <= 0 ||
            slot.TimeFrom is not { Kind: DateTimeKind.Unspecified } from ||
            slot.TimeTo is not { Kind: DateTimeKind.Unspecified } to ||
            from.Date != DateTime.MinValue || to.Date != DateTime.MinValue || to <= from)
            return false;

        start = TimeOnly.FromDateTime(from);
        end = TimeOnly.FromDateTime(to);
        return true;
    }
}
