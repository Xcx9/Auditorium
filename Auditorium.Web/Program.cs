using Auditorium.Infrastructure.Scheduling;
using Auditorium.Core.DTOs;
using Auditorium.Core.Interfaces;
using Auditorium.Infrastructure.Time;
using Auditorium.Core.Services;
using Auditorium.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var refreshSchedule = args.FirstOrDefault() == "refresh-schedule";
var builder = WebApplication.CreateBuilder(refreshSchedule ? [] : args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<IScheduleParser, ScheduleParser>(client =>
    client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IClock, MoscowClock>();
builder.Services.AddScoped<ICampusStore, CampusStore>();
builder.Services.AddScoped<CampusService>();
builder.Services.AddScoped<IScheduleSnapshotStore, ScheduleSnapshotStore>();
builder.Services.AddScoped<ScheduleUpdater>();

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")));

await using var app = builder.Build();

if (refreshSchedule)
{
    if (args.Length != 4 || !DateOnly.TryParseExact(args[2], "yyyy-MM-dd", out var from) ||
        !DateOnly.TryParseExact(args[3], "yyyy-MM-dd", out var through))
    {
        Console.Error.WriteLine("РСЃРїРѕР»СЊР·РѕРІР°РЅРёРµ: refresh-schedule <СЃРµРјРµСЃС‚СЂ> <Р“Р“Р“Р“-РњРњ-Р”Р”> <Р“Р“Р“Р“-РњРњ-Р”Р”>");
        Environment.ExitCode = 2;
        return;
    }

    await using var scope = app.Services.CreateAsyncScope();
    var updater = scope.ServiceProvider.GetRequiredService<ScheduleUpdater>();
    var result = await updater.Refresh(new ScheduleLoadRequest(args[1], from, through));
    if (result.Succeeded)
        Console.WriteLine("Р Р°СЃРїРёСЃР°РЅРёРµ СЃРѕС…СЂР°РЅРµРЅРѕ.");
    else
    {
        Console.Error.WriteLine(result.Error);
        Environment.ExitCode = 1;
    }

    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

/*using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    Console.WriteLine();
    Console.WriteLine("============================================");
    Console.WriteLine(" МИЭТ: импорт расписания по аудиториям");
    Console.WriteLine("============================================");

    try
    {
        await db.Database.MigrateAsync();

        using var parser = new ScheduleParser();

        var lessons =
            await parser.ParseAllScheduleAsync();

        var audiences =
            parser.ExtractAudiences(lessons);

        var byAudience =
            parser.GroupLessonsByAudience(lessons);

        Console.WriteLine();
        Console.WriteLine(
            $"Получено занятий:   {lessons.Count}");

        Console.WriteLine(
            $"Получено аудиторий: {audiences.Count}");

        Console.WriteLine();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        db.Lessons.RemoveRange(
            db.Lessons);

        db.Audiences.RemoveRange(
            db.Audiences);

        await db.SaveChangesAsync();

        db.Audiences.AddRange(
            audiences);

        await db.SaveChangesAsync();

        db.Lessons.AddRange(
            lessons);

        await db.SaveChangesAsync();

        await transaction.CommitAsync();

        Console.WriteLine(
            $"В БД аудиторий: {audiences.Count}");

        Console.WriteLine(
            $"В БД занятий: {lessons.Count}");

        Console.WriteLine();
        Console.WriteLine("Пример:");

        foreach (var audience in audiences.Take(10))
        {
            byAudience.TryGetValue(
                audience.Id,
                out var roomLessons);

            roomLessons ??= [];

            Console.WriteLine(
                $"{audience.Id} | " +
                $"{audience.Name} | " +
                $"занятий: {roomLessons.Count}");
        }

        Console.WriteLine();
        Console.WriteLine("Импорт завершён.");
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine("ОШИБКА ИМПОРТА:");
        Console.WriteLine(ex);
    }

    Console.WriteLine("============================================");
    Console.WriteLine();
}
*/
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();