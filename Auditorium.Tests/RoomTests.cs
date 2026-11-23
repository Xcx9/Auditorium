using Auditorium.Core.Entities;
using Auditorium.Infrastructure.Time;
using NUnit.Framework;

namespace Auditorium.Tests;

public class RoomTests
{
    [TestCase("3120", "3")]
    [TestCase("3102а", "3")]
    [TestCase("3120 а (к)", "3")]
    [TestCase("3209 МПСУ", "3")]
    [TestCase(" 4102 (м) ", "4")]
    public void AcceptsNumericRoomsAndNormalizesWhitespace(string number, string building)
    {
        var room = new Room(number);
        Assert.That(room.Number, Is.EqualTo(number.Trim()));
        Assert.That(room.Building, Is.EqualTo(building));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("312")]
    [TestCase("12345")]
    [TestCase("0123")]
    [TestCase("٣١٢٠")]
    [TestCase("3120\n")]
    [TestCase("УВЦ 1")]
    public void RejectsInvalidOrNamedRooms(string? number)
    {
        Assert.That(Room.IsValidNumber(number), Is.False);
        Assert.Throws<ArgumentException>(() => new Room(number!));
    }

    [Test]
    public void MoscowDateCanDifferFromUtcDate()
    {
        var clock = new MoscowClock(new FixedTime());
        Assert.That(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), Is.EqualTo(new DateOnly(2026, 11, 22)));
        Assert.That(clock.Today, Is.EqualTo(new DateOnly(2026, 11, 23)));
        Assert.That(clock.GetDate(clock.UtcNow), Is.EqualTo(clock.Today));
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 22, 21, 30, 0, TimeSpan.Zero);
    }

    [Test]
    public void RenameUsesSameRulesAndRecalculatesBuilding()
    {
        var room = new Room("3120");
        room.ApplySchedule(120, " 4102 (м) ", [new DateOnly(2026, 11, 23)]);
        Assert.That(room.Number, Is.EqualTo("4102 (м)"));
        Assert.That(room.Building, Is.EqualTo("4"));
        Assert.Throws<ArgumentException>(() => room.ApplySchedule(120, "УВЦ 1", []));
        Assert.That(room.Number, Is.EqualTo("4102 (м)"));
    }
}
