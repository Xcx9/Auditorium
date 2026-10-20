using Auditorium.Core.Services;
using Auditorium.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

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