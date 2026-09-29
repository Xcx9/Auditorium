using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Auditorium.Core.DTOs;
using Auditorium.Core.Entities;

namespace Auditorium.Core.Services
{
    public class ScheduleParser
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://miet.ru/schedule/data?audience=";

        public ScheduleParser()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        /// <summary>
        /// Загружает и парсит расписание для указанной аудитории.
        /// </summary>
        /// <param name="audienceId">Числовой код аудитории (например, 120).</param>
        /// <returns>Список занятий.</returns>
        public async Task<List<Lesson>> ParseScheduleByAudienceAsync(int audienceId)
        {
            var lessons = new List<Lesson>();
            string url = $"{BaseUrl}{audienceId}";

            try
            {
                string json = await _httpClient.GetStringAsync(url);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var scheduleData = JsonSerializer.Deserialize<ScheduleResponse>(json, options);

                if (scheduleData?.Data == null)
                {
                    Console.WriteLine("Данные о расписании не найдены.");
                    return lessons;
                }

                foreach (var item in scheduleData.Data)
                {
                    var lesson = new Lesson
                    {
                        Day = item.Day,
                        DayNumber = item.DayNumber,
                        Time = item.Time?.Time,
                        TimeFrom = item.Time?.TimeFrom,
                        TimeTo = item.Time?.TimeTo,
                        Subject = item.Class?.Name,
                        TeacherFull = item.Class?.TeacherFull,
                        Teacher = item.Class?.Teacher,
                        GroupName = item.Group?.Name,
                        RoomCode = item.Room?.Code,
                        RoomName = item.Room?.Name,
                        AudienceId = audienceId
                    };
                    lessons.Add(lesson);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при парсинге расписания: {ex.Message}");
            }

            return lessons;
        }

        /// <summary>
        /// Оставлен для совместимости: фильтрация по названию аудитории.
        /// </summary>
        public List<Lesson> FilterByRoom(List<Lesson> allLessons, string roomName)
        {
            var result = new List<Lesson>();
            foreach (var l in allLessons)
            {
                if (!string.IsNullOrEmpty(l.RoomName) &&
                    l.RoomName.Contains(roomName, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(l);
                }
            }
            return result;
        }
    }
}