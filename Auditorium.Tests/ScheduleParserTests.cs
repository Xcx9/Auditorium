using System.Net;
using Auditorium.Core.DTOs;
using Auditorium.Infrastructure.Scheduling;
using NUnit.Framework;

namespace Auditorium.Tests;

public class ScheduleParserTests
{
    private const string Semester = "Осенний семестр 2026/2027";
    private static readonly ScheduleLoadRequest Request =
        new(Semester, new DateOnly(2026, 11, 7), new DateOnly(2026, 11, 9));

    private static string ReadFixture(string file) =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", file));

    [Test]
    public async Task LoadsFourWeekCycleAndRetainsCoverageForEmptyRooms()
    {
        using var client = new HttpClient(new MietHandler());
        var result = await new ScheduleParser(client).LoadAndValidate(Request);

        Assert.That(result, Is.TypeOf<ScheduleLoadResult.Loaded>());
        var rooms = ((ScheduleLoadResult.Loaded)result).Rooms;
        Assert.That(rooms, Has.Count.EqualTo(2));
        Assert.That(rooms.Select(x => x.SourceRoomCode), Is.EqualTo(new[] { 120, 126 }));
        Assert.That(rooms.Select(x => x.Room.Number), Is.EqualTo(new[] { "3201 (м)", "4102 (м)" }));
        var expectedDays = new[] { Request.From, Request.From.AddDays(1), Request.Through };
        Assert.That(rooms.All(x => x.Room.ScheduleDates.SequenceEqual(expectedDays)), Is.True);
        Assert.That(rooms[1].Lessons, Is.Empty);
        Assert.That(rooms[0].Lessons.Select(x => x.StartsAt), Is.EqualTo(new[]
        {
            new DateTimeOffset(2026, 11, 8, 12, 30, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.FromHours(3))
        }));
        Assert.That(rooms[0].Lessons.All(x => ReferenceEquals(x.Room, rooms[0].Room)), Is.True);
    }

    [TestCase("{}", ScheduleLoadError.InvalidResponse)]
    [TestCase("{malformed", ScheduleLoadError.InvalidResponse)]
    [TestCase("[]", ScheduleLoadError.InvalidResponse)]
    [TestCase("partial", ScheduleLoadError.InvalidResponse)]
    [TestCase("unavailable", ScheduleLoadError.SourceUnavailable)]
    public async Task RejectsPartialLoadWhenSecondRoomFails(string secondRoom, ScheduleLoadError expected)
    {
        using var client = new HttpClient(new MietHandler(secondRoom));
        var result = await new ScheduleParser(client).LoadAndValidate(Request);
        Assert.That(result, Is.InstanceOf<ScheduleLoadResult.Failed>());
        Assert.That(((ScheduleLoadResult.Failed)result).Error, Is.EqualTo(expected));
    }

    [Test]
    public async Task RejectsDifferentSemester()
    {
        using var client = new HttpClient(new MietHandler());
        var result = await new ScheduleParser(client).LoadAndValidate(Request with { Semester = "Другой семестр" });
        Assert.That(result, Is.InstanceOf<ScheduleLoadResult.Failed>());
    }

    [Test]
    public void PropagatesCallerCancellation()
    {
        using var client = new HttpClient(new MietHandler());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () =>
            await new ScheduleParser(client).LoadAndValidate(Request, cancellation.Token));
    }

    [Test]
    public async Task RejectsCatalogWithoutRoomsInSupportedBuildings()
    {
        using var client = new HttpClient(new MietHandler(catalog: """[{"Code":243,"Name":"8110"}]"""));
        var result = await new ScheduleParser(client).LoadAndValidate(Request);
        Assert.That(result, Is.TypeOf<ScheduleLoadResult.Failed>());
        Assert.That(((ScheduleLoadResult.Failed)result).Error, Is.EqualTo(ScheduleLoadError.InvalidResponse));
    }

    private sealed class MietHandler(string? secondRoom = null, string? catalog = null) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/schedule/" when request.Method == HttpMethod.Get:
                    return Reply("""<script>var semestr_start = "2026-08-04"</script>""");
                case "/schedule/audiences" when request.Method == HttpMethod.Post:
                    return Reply(catalog ?? ReadFixture("miet-audiences.json"));
                case "/schedule/data" when request.Method == HttpMethod.Post:
                    var form = (MultipartFormDataContent)request.Content!;
                    var field = form.Single();
                    Assert.That(field.Headers.ContentDisposition?.Name?.Trim('"'), Is.EqualTo("audience"));
                    var code = await field.ReadAsStringAsync(cancellationToken);
                    if (code == "120")
                        return Reply(ReadFixture("miet-room.json"));
                    Assert.That(code, Is.EqualTo("126"));
                    if (secondRoom == "unavailable")
                        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                    var response = Reply(secondRoom is null or "partial" ? """
                            {"Times":[{"Code":3,"TimeFrom":"0001-01-01T12:00:00","TimeTo":"0001-01-01T13:20:00"}],
                             "Data":[],"Semestr":"Осенний семестр 2026/2027"}
                            """ : secondRoom);
                    if (secondRoom == "partial")
                        response.StatusCode = HttpStatusCode.PartialContent;
                    return response;
                default:
                    throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}");
            }
        }

        private static HttpResponseMessage Reply(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body) };
    }
}
