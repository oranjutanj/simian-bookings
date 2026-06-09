using SimianBookings.Models;
using SimianBookings.Services;

namespace SimianBookings.Tests.Services;

public class SlotCalculatorTests
{
    [Fact]
    public void GetAvailableSlots_ExcludesBusyAndRespectsBuffer()
    {
        var availabilityWindows = new List<AvailabilityWindow>
        {
            new(["Monday"], "18:00", "21:00")
        };

        var session = new SessionType(
            "coaching-45",
            "Coaching",
            "desc",
            45,
            15);

        var fromUtc = NextUtcDay(DayOfWeek.Monday);
        var toUtc = fromUtc.AddDays(1);
        var uk = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        var localDay = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, uk).Date;

        var slot18 = TimeZoneInfo.ConvertTimeToUtc(localDay.AddHours(18), uk);
        var slot19 = TimeZoneInfo.ConvertTimeToUtc(localDay.AddHours(19), uk);
        var slot20 = TimeZoneInfo.ConvertTimeToUtc(localDay.AddHours(20), uk);

        // Busy period ends at 18:50. With a 15-minute buffer applied before the new slot,
        // the 19:00 slot is also blocked (only 10 min gap — less than the 15-min buffer).
        // The first available slot is 20:00 (70 min gap > 15 min buffer).
        var busy = new List<(DateTime Start, DateTime End)>
        {
            (slot18, slot18.AddMinutes(50))
        };

        var result = SlotCalculator.GetAvailableSlots(session, availabilityWindows, busy, fromUtc, toUtc, minNoticeHours: 0);

        Assert.DoesNotContain(slot18, result);
        Assert.DoesNotContain(slot19, result); // only 10-min gap before slot — less than 15-min buffer
        Assert.Contains(slot20, result);
    }

    [Fact]
    public void GetAvailableSlots_BlocksSlotStartingImmediatelyAfterExistingSession()
    {
        // Reproduces: existing session 19:30-20:30, buffer 5 min.
        // A new 60-min slot at 20:30 must be blocked (zero gap).
        // The first available slot should be 20:35.
        var availabilityWindows = new List<AvailabilityWindow>
        {
            new(["Monday"], "19:30", "21:30")
        };

        var session = new SessionType(
            "coaching-60",
            "Coaching 60",
            "desc",
            60,
            5,
            SlotIntervalMinutes: 5);

        var fromUtc = NextUtcDay(DayOfWeek.Monday);
        var toUtc = fromUtc.AddDays(1);
        var uk = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        var localDay = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, uk).Date;

        var existingStart = TimeZoneInfo.ConvertTimeToUtc(localDay.AddHours(19).AddMinutes(30), uk);
        var existingEnd   = TimeZoneInfo.ConvertTimeToUtc(localDay.AddHours(20).AddMinutes(30), uk);
        var slot2030      = TimeZoneInfo.ConvertTimeToUtc(localDay.AddHours(20).AddMinutes(30), uk);
        var slot2035      = TimeZoneInfo.ConvertTimeToUtc(localDay.AddHours(20).AddMinutes(35), uk);

        var busy = new List<(DateTime Start, DateTime End)> { (existingStart, existingEnd) };

        var result = SlotCalculator.GetAvailableSlots(session, availabilityWindows, busy, fromUtc, toUtc, minNoticeHours: 0);

        Assert.DoesNotContain(slot2030, result); // zero gap — must be blocked
        Assert.Contains(slot2035, result);        // 5-min gap exactly matches buffer — available
    }

    [Fact]
    public void GetAvailableSlots_AppliesMinimumNotice()
    {
        var availabilityWindows = new List<AvailabilityWindow>
        {
            new([DateTime.UtcNow.DayOfWeek.ToString()], "18:00", "21:00")
        };

        var session = new SessionType(
            "coaching-45",
            "Coaching",
            "desc",
            45,
            15);

        var fromUtc = DateTime.UtcNow.Date;
        var toUtc = fromUtc.AddDays(1);
        var minBookable = DateTime.UtcNow.AddHours(24);

        var result = SlotCalculator.GetAvailableSlots(session, availabilityWindows, [], fromUtc, toUtc, minNoticeHours: 24);

        Assert.DoesNotContain(result, s => s < minBookable);
    }

    [Fact]
    public void GetAvailableSlots_UsesGmtOffsetDuringWinter()
    {
        var availabilityWindows = new List<AvailabilityWindow>
        {
            new(["Monday"], "18:00", "19:00")
        };

        var session = new SessionType(
            "coaching-45",
            "Coaching",
            "desc",
            45,
            15);

        var fromUtc = NextUtcDateInMonth(DayOfWeek.Monday, 1);
        var toUtc = fromUtc;
        var uk = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        var localDay = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, uk).Date;
        var expectedSlot = TimeZoneInfo.ConvertTimeToUtc(localDay.AddHours(18), uk);

        var result = SlotCalculator.GetAvailableSlots(session, availabilityWindows, [], fromUtc, toUtc, minNoticeHours: 0);

        Assert.Contains(expectedSlot, result);
    }

    [Fact]
    public void GetAvailableSlots_UsesBstOffsetDuringSummer()
    {
        var availabilityWindows = new List<AvailabilityWindow>
        {
            new(["Monday"], "18:00", "19:00")
        };

        var session = new SessionType(
            "coaching-45",
            "Coaching",
            "desc",
            45,
            15);

        var fromUtc = NextUtcDateInMonth(DayOfWeek.Monday, 7);
        var toUtc = fromUtc;
        var uk = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        var localDay = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, uk).Date;
        var expectedSlot = TimeZoneInfo.ConvertTimeToUtc(localDay.AddHours(18), uk);

        var result = SlotCalculator.GetAvailableSlots(session, availabilityWindows, [], fromUtc, toUtc, minNoticeHours: 0);

        Assert.Contains(expectedSlot, result);
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

    private static DateTime NextUtcDateInMonth(DayOfWeek targetDay, int month)
    {
        var minDate = DateTime.UtcNow.Date.AddDays(7);

        for (var year = DateTime.UtcNow.Year; year <= DateTime.UtcNow.Year + 10; year++)
        {
            var day = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            while (day.DayOfWeek != targetDay)
            {
                day = day.AddDays(1);
            }

            if (day >= minDate)
            {
                return day;
            }
        }

        throw new InvalidOperationException("Could not find a future date for the requested month and day.");
    }
}
