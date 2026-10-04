using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Auditorium.Core.DTOs;
using Auditorium.Core.Entities;

// TODO: Просмотреть ошибки в логах, доработать обработку данных
namespace Auditorium.Core.Services;

public sealed class ScheduleParser : IDisposable
{
    private readonly string _logDirectory;
    private readonly string _logFile;
    private readonly object _logLock = new();

    private const string SchedulePageUrl = "https://miet.ru/schedule/";
    private const string GroupsUrl = "https://miet.ru/schedule/groups";
    private const string DataUrl = "https://miet.ru/schedule/data";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;

    public ScheduleParser(HttpClient? httpClient = null)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _disposeHttpClient = false;
        }
        else
        {
            var handler = new HttpClientHandler
            {
                UseCookies = false,
                AllowAutoRedirect = true,
                AutomaticDecompression =
                    DecompressionMethods.GZip |
                    DecompressionMethods.Deflate |
                    DecompressionMethods.Brotli
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            _disposeHttpClient = true;
        }

        ConfigureHttpClient();

        _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

        Directory.CreateDirectory(_logDirectory);

        _logFile = Path.Combine(_logDirectory, $"miet-parser-{DateTime.Now:yyyy-MM-dd}.log");

        WriteLog("========== ЗАПУСК ПАРСЕРА ==========");
    }

    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.Clear();

        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/140.0.0.0 Safari/537.36");

        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "Accept",
            "*/
*");

        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "Accept-Language",
            "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
    }

    // ============================================================
    // ПОЛУЧЕНИЕ СПИСКА ГРУПП
    // ============================================================

    public async Task<List<string>> GetGroupCodesAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            GroupsUrl);

        request.Headers.TryAddWithoutValidation(
            "Referer",
            SchedulePageUrl);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(
            cancellationToken);

        Console.WriteLine(
            $"[МИЭТ] GET /schedule/groups -> {(int)response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Не удалось получить список групп. " +
                $"HTTP {(int)response.StatusCode}: {body}");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException(
                "МИЭТ вернул пустой список групп.");
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            var groups = ExtractGroups(document.RootElement);

            Console.WriteLine(
                $"[МИЭТ] Найдено групп: {groups.Count}");

            return groups;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Не удалось разобрать /schedule/groups как JSON.\n" +
                $"Ответ сервера:\n{Preview(body)}",
                ex);
        }
    }

    // ============================================================
    // ОСНОВНОЙ МЕТОД
    // ============================================================

    public async Task<List<Lesson>> ParseAllScheduleAsync(
        CancellationToken cancellationToken = default)
    {
        var groups = await GetGroupCodesAsync(cancellationToken);

        if (groups.Count == 0)
        {
            throw new InvalidOperationException(
                "Список групп пуст.");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"[МИЭТ] Начинаем загрузку расписания для {groups.Count} групп...");
        Console.WriteLine();

        var allLessons = new List<Lesson>();

        var processed = 0;
        var failed = 0;

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var schedule = await GetScheduleForGroupAsync(
                            group,
                            cancellationToken);

                var lessons = ConvertToLessons(
                        schedule,
                        group);

                allLessons.AddRange(lessons);
            }
            catch (Exception ex)
            {
                failed++;

                Console.WriteLine(
                    $"[МИЭТ] ОШИБКА группы '{group}':");

                Console.WriteLine(
                    $"        {ex.Message}");

                WriteLog(
                    $"ОШИБКА ГРУППЫ | " +
                    $"Группа='{group}' | " +
                    $"Ошибка='{ex.Message}'");

                WriteLog(
                    $"STACK TRACE | " +
                    $"{ex}");
            }

            processed++;

            if (processed % 10 == 0 ||
                processed == groups.Count)
            {
                Console.WriteLine(
                    $"[МИЭТ] Обработано: " +
                    $"{processed}/{groups.Count}, " +
                    $"ошибок: {failed}, " +
                    $"занятий: {allLessons.Count}");
            }

            // Чтобы не долбить сервер слишком быстро.
            await Task.Delay(
                TimeSpan.FromMilliseconds(150),
                cancellationToken);
        }

        // ========================================================
        // УДАЛЕНИЕ ДУБЛИКАТОВ
        // ========================================================

        var uniqueLessons = allLessons
            .GroupBy(BuildLessonKey)
            .Select(x => x.First())
            .OrderBy(x => x.RoomCode)
            .ThenBy(x => x.Day)
            .ThenBy(x => x.DayNumber)
            .ThenBy(x => x.TimeFrom)
            .ToList();

        Console.WriteLine();
        Console.WriteLine(
            $"[МИЭТ] Всего получено занятий: {uniqueLessons.Count}");

        var audienceCount = uniqueLessons
            .Where(x => x.AudienceId.HasValue)
            .Select(x => x.AudienceId!.Value)
            .Distinct()
            .Count();

        Console.WriteLine(
            $"[МИЭТ] Всего аудиторий: {audienceCount}");

        Console.WriteLine(
            $"[МИЭТ] Ошибок групп: {failed}");

        if (uniqueLessons.Count == 0)
        {
            throw new InvalidOperationException(
                "МИЭТ не вернул ни одного занятия с аудиторией.\n" +
                "База данных не будет очищена.");
        }

        return uniqueLessons;
    }

    // ============================================================
    // ПОЛУЧЕНИЕ РАСПИСАНИЯ ОДНОЙ ГРУППЫ
    // ============================================================

    private async Task<ScheduleResponse> GetScheduleForGroupAsync(
        string group,
        CancellationToken cancellationToken)
    {
        // --------------------------------------------------------
        // ПЕРВЫЙ ЗАПРОС
        // --------------------------------------------------------

        using var firstResponse =
            await SendScheduleRequestAsync(
                group,
                cookie: null,
                cancellationToken);



        var firstBody =
            await firstResponse.Content.ReadAsStringAsync(
                cancellationToken);

        WriteLog(
            $"HTTP | " +
            $"Группа='{group}' | " +
            $"Статус={(int)firstResponse.StatusCode}");

        WriteLog(
            $"CONTENT-TYPE | " +
            $"Группа='{group}' | " +
            $"{firstResponse.Content.Headers.ContentType}");

        Console.WriteLine(
            $"[МИЭТ] '{group}' -> " +
            $"первый ответ HTTP {(int)firstResponse.StatusCode}");

        // Иногда сервер сразу возвращает JSON.
        if (LooksLikeJson(firstBody))
        {
            if (!firstResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"HTTP {(int)firstResponse.StatusCode}: " +
                    Preview(firstBody));
            }

            return DeserializeSchedule(
                firstBody,
                group);
        }

        // --------------------------------------------------------
        // ПОЛУЧАЕМ WL
        // --------------------------------------------------------

        var cookie =
            ExtractWlFromBody(firstBody);

        if (cookie == null)
        {
            cookie =
                ExtractWlFromHeaders(firstResponse);
            WriteLog(
                $"WL NOT FOUND | " +
                $"Группа='{group}' | " +
                $"Ответ='{Preview(firstBody)}'");

            throw new InvalidOperationException(
                "Сервер не вернул JSON и не удалось найти cookie wl.\n" +
                $"Ответ сервера:\n{Preview(firstBody)}");
        }

        Console.WriteLine(
            $"[МИЭТ] '{group}' -> получен wl, делаем второй запрос");

        // --------------------------------------------------------
        // ВТОРОЙ ЗАПРОС
        // --------------------------------------------------------

        using var secondResponse =
            await SendScheduleRequestAsync(
                group,
                cookie,
                cancellationToken);

        var secondBody =
            await secondResponse.Content.ReadAsStringAsync(
                cancellationToken);

        Console.WriteLine(
            $"[МИЭТ] '{group}' -> " +
            $"второй ответ HTTP {(int)secondResponse.StatusCode}");

        if (!secondResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Второй запрос вернул HTTP " +
                $"{(int)secondResponse.StatusCode}.\n" +
                $"Ответ:\n{Preview(secondBody)}");
        }

        if (!LooksLikeJson(secondBody))
        {
            throw new InvalidOperationException(
                "Второй запрос должен вернуть JSON, " +
                "но пришёл другой ответ.\n" +
                $"Ответ:\n{Preview(secondBody)}");
        }

        return DeserializeSchedule(
            secondBody,
            group);
    }

    // ============================================================
    // POST /schedule/data
    // ============================================================

    private async Task<HttpResponseMessage> SendScheduleRequestAsync(
        string group,
        string? cookie,
        CancellationToken cancellationToken)
    {
        var content =
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["group"] = group
                });

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                DataUrl);

        request.Content = content;

        request.Headers.TryAddWithoutValidation(
            "Referer",
            SchedulePageUrl);

        request.Headers.TryAddWithoutValidation(
            "Origin",
            "https://miet.ru");

        request.Headers.TryAddWithoutValidation(
            "X-Requested-With",
            "XMLHttpRequest");

        if (!string.IsNullOrWhiteSpace(cookie))
        {
            request.Headers.TryAddWithoutValidation(
                "Cookie",
                cookie);
        }

        return await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    // ============================================================
    // JSON -> ScheduleResponse
    // ============================================================

    private ScheduleResponse DeserializeSchedule(
        string json,
        string group)
    {
        try
        {
            var result =
                JsonSerializer.Deserialize<ScheduleResponse>(
                    json,
                    JsonOptions);

            if (result == null)
            {
                throw new JsonException(
                    "JSON преобразовался в null.");
            }

            if (result.Data == null)
            {
                throw new JsonException(
                    "В JSON отсутствует поле Data.");
            }

            return result;
        }
        catch (Exception ex)
        {
            WriteLog(
                $"JSON ERROR | " +
                $"Группа='{group}' | " +
                $"Тип='{ex.GetType().FullName}' | " +
                $"Ошибка='{ex.Message}'");

            WriteLog(
                $"JSON PREVIEW | " +
                $"Группа='{group}' | " +
                $"{Preview(json)}");

            throw new InvalidOperationException(
                $"Ошибка разбора расписания группы '{group}'.\n" +
                $"Тип ошибки: {ex.GetType().FullName}\n" +
                $"Сообщение: {ex.Message}\n" +
                $"JSON:\n{Preview(json)}",
                ex);
        }
    }

    // ============================================================
    // DTO -> Entity
    // ============================================================

    private static List<Lesson> ConvertToLessons(
    ScheduleResponse schedule,
    string group)
    {
        if (schedule.Data == null)
            return [];

        return schedule.Data
            .Where(x => x.Room?.Name != null)
            .Select(x => new Lesson
            {
                Day = x.Day,
                DayNumber = x.DayNumber,

                Time = x.Time?.Time,
                TimeFrom = x.Time?.TimeFrom,
                TimeTo = x.Time?.TimeTo,

                Subject = x.Class?.Name,
                TeacherFull = x.Class?.TeacherFull,
                Teacher = x.Class?.Teacher,

                // Группу берём из параметра запроса,
                // а не из JSON.
                GroupName = group,

                RoomCode = x.Room?.Code,
                RoomName = x.Room?.Name,

                AudienceId = x.Room?.Code,

                // Пока не берём Semestr из JSON.
                Semester = null
            })
            .ToList();
    }

    // ============================================================
    // ГРУППИРОВКА ПО АУДИТОРИЯМ
    // ============================================================

    public Dictionary<int, List<Lesson>> GroupLessonsByAudience(
        IEnumerable<Lesson> lessons)
    {
        return lessons
            .Where(x => x.AudienceId.HasValue)
            .GroupBy(x => x.AudienceId!.Value)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderBy(y => y.Day)
                    .ThenBy(y => y.DayNumber)
                    .ThenBy(y => y.TimeFrom)
                    .ToList());
    }

    // ============================================================
    // СОЗДАНИЕ СПИСКА AUDIENCE
    // ============================================================

    public List<Audience> ExtractAudiences(
        IEnumerable<Lesson> lessons)
    {
        return lessons
            .Where(x => x.AudienceId.HasValue)
            .GroupBy(x => x.AudienceId!.Value)
            .Select(x => new Audience
            {
                Id = x.Key,

                Name = x
                    .Select(y => y.RoomName)
                    .FirstOrDefault(y =>
                        !string.IsNullOrWhiteSpace(y))
            })
            .OrderBy(x => x.Id)
            .ToList();
    }

    // ============================================================
    // ПОИСК АУДИТОРИИ
    // ============================================================

    public List<Audience> SearchAudiences(
        IEnumerable<Audience> audiences,
        string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return audiences
                .OrderBy(x => x.Id)
                .ToList();
        }

        query = query.Trim();

        return audiences
            .Where(x =>
                x.Id.ToString()
                    .Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase)
                ||
                (!string.IsNullOrWhiteSpace(x.Name) &&
                 x.Name.Contains(
                     query,
                     StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.Id)
            .ToList();
    }

    // ============================================================
    // РАСПИСАНИЕ КОНКРЕТНОЙ АУДИТОРИИ
    // ============================================================

    public List<Lesson> GetScheduleByAudience(
        IEnumerable<Lesson> lessons,
        int audienceId)
    {
        return lessons
            .Where(x => x.AudienceId == audienceId)
            .OrderBy(x => x.Day)
            .ThenBy(x => x.DayNumber)
            .ThenBy(x => x.TimeFrom)
            .ToList();
    }

    // ============================================================
    // ПОСТРОЕНИЕ КЛЮЧА ДЛЯ ДЕДУПЛИКАЦИИ
    // ============================================================

    private static string BuildLessonKey(
        Lesson lesson)
    {
        return string.Join(
            "|",
            lesson.Day,
            lesson.DayNumber,
            lesson.Time,
            lesson.TimeFrom,
            lesson.TimeTo,
            lesson.Subject,
            lesson.TeacherFull,
            lesson.Teacher,
            lesson.GroupName,
            lesson.RoomCode,
            lesson.RoomName,
            lesson.Semester);
    }

    // ============================================================
    // ИЗВЛЕЧЕНИЕ ГРУПП
    // ============================================================

    private static List<string> ExtractGroups(
        JsonElement root)
    {
        var result =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        ExtractGroupsRecursive(
            root,
            result);

        return result
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    private static void ExtractGroupsRecursive(
        JsonElement element,
        HashSet<string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:

                foreach (var item in element.EnumerateArray())
                {
                    ExtractGroupsRecursive(
                        item,
                        result);
                }

                break;

            case JsonValueKind.Object:

                // code
                if (element.TryGetProperty(
                        "code",
                        out var code))
                {
                    if (code.ValueKind ==
                        JsonValueKind.String)
                    {
                        var value =
                            code.GetString();

                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            result.Add(value.Trim());
                        }
                    }

                    return;
                }

                // group
                if (element.TryGetProperty(
                        "group",
                        out var group))
                {
                    ExtractGroupsRecursive(
                        group,
                        result);

                    return;
                }

                // groups
                if (element.TryGetProperty(
                        "groups",
                        out var groups))
                {
                    ExtractGroupsRecursive(
                        groups,
                        result);

                    return;
                }

                // data
                if (element.TryGetProperty(
                        "data",
                        out var data))
                {
                    ExtractGroupsRecursive(
                        data,
                        result);

                    return;
                }

                break;

            case JsonValueKind.String:

                var stringValue =
                    element.GetString();

                if (!string.IsNullOrWhiteSpace(
                        stringValue))
                {
                    result.Add(
                        stringValue.Trim());
                }

                break;
        }
    }

    // ============================================================
    // WL
    // ============================================================

    private static string? ExtractWlFromBody(
        string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var match = Regex.Match(
            body,
            @"wl=[^;""'\s]+;path=/",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        return match.Value;
    }

    private static string? ExtractWlFromHeaders(
        HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(
                "Set-Cookie",
                out var values))
        {
            return null;
        }

        foreach (var value in values)
        {
            var match = Regex.Match(
                value,
                @"wl=[^;""'\s]+",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
    }

    // ============================================================
    // ПРОВЕРКА JSON
    // ============================================================

    private static bool LooksLikeJson(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed =
            text.TrimStart();

        return trimmed.StartsWith("{") ||
               trimmed.StartsWith("[");
    }

    // ============================================================
    // ОТРЕЗОК ОТВЕТА ДЛЯ ЛОГА
    // ============================================================

    private static string Preview(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "<пустой ответ>";

        text = text
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        return text.Length > 1000
            ? text[..1000] + "..."
            : text;
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private void WriteLog(string message)
    {
        var line =
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";

        lock (_logLock)
        {
            File.AppendAllText(
                _logFile,
                line + Environment.NewLine,
                Encoding.UTF8);
        }
    }
}