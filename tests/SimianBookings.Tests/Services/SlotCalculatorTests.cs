using SimianBookings.Models;
using SimianBookings.Services;

namespace SimianBookings.Tests.Services;

public class SlotCalculatorTests
{
    [Fact]
    public void GetAvailableSlots_ExcludesBusyAndRespectsBuffer()
    {
        var session = new SessionType(
            "coaching-45",
            "Coaching",
            "desc",
            45,
            15,
            [
                new AvailabilityWindow(["Monday"], "18:00", "21:00")
            ]);

        var fromUtc = NextUtcDay(DayOfWeek.Monday);
        var toUtc = fromUtc.AddDays(1);
        var uk = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        var localDay = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, uk).Date;

        var slot18 = TimeZoneInfo.ConvertTimeToUtc(localDay.AddHours(18), uk);
        var slot19 = TimeZoneInfo.ConvertTimeToUtc(localDay.AddHours(19), uk);
        var slot20 = TimeZoneInfo.ConvertTimeToUtc(localDay.AddHours(20), uk);

        // Busy period blocks the 18:00 slot and its 15-minute buffer.
        var busy = new List<(DateTime Start, DateTime End)>
        {
            (slot18, slot18.AddMinutes(50))
        };

        var result = SlotCalculator.GetAvailableSlots(session, busy, fromUtc, toUtc, minNoticeHours: 0);

        Assert.DoesNotContain(slot18, result);
        Assert.Contains(slot19, result);
        Assert.Contains(slot20, result);
    }

    [Fact]
    public void GetAvailableSlots_AppliesMinimumNotice()
    {
        var session = new SessionType(
            "coaching-45",
            "Coaching",
            "desc",
            45,
            15,
            [
                new AvailabilityWindow([DateTime.UtcNow.DayOfWeek.ToString()], "18:00", "21:00")
            ]);

        var fromUtc = DateTime.UtcNow.Date;
        var toUtc = fromUtc.AddDays(1);
        var minBookable = DateTime.UtcNow.AddHours(24);

        var result = SlotCalculator.GetAvailableSlots(session, [], fromUtc, toUtc, minNoticeHours: 24);

        Assert.DoesNotContain(result, s => s < minBookable);
    }

    private static DateTime NextUtcDay(DayOfWeek targetDay)
    {
        var day = DateTime.UtcNow.Date.AddDays(1);
        while (day.DayOfWeek != targetDay)
        {
            day = day.AddDays(1);
        }

        return day;
    }
}
