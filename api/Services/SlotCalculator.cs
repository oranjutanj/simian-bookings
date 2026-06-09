using SimianBookings.Models;

namespace SimianBookings.Services;

public static class SlotCalculator
{
    private static readonly TimeZoneInfo UkTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

    /// <summary>
    /// Given a session type, busy UTC slots, and a UTC date range,
    /// returns a list of available slot start times (UTC).
    /// </summary>
    public static List<DateTime> GetAvailableSlots(
        SessionType session,
        List<AvailabilityWindow> availabilityWindows,
        List<(DateTime Start, DateTime End)> busyUtcSlots,
        DateTime fromUtc,
        DateTime toUtc,
        int minNoticeHours = 24)
    {
        var available = new List<DateTime>();
        var minBookableUtc = DateTime.UtcNow.AddHours(minNoticeHours);

        for (var day = fromUtc.Date; day <= toUtc.Date; day = day.AddDays(1))
        {
            // Convert day to UK local time for window matching
            var dayLocal = TimeZoneInfo.ConvertTimeFromUtc(day, UkTimeZone);
            var dayName = dayLocal.DayOfWeek.ToString();

            var windows = availabilityWindows
                .Where(w => w.DaysOfWeek.Contains(dayName))
                .ToList();

            foreach (var window in windows)
            {
                var windowStartLocal = dayLocal.Date + TimeSpan.Parse(window.StartTime);
                var windowEndLocal = dayLocal.Date + TimeSpan.Parse(window.EndTime);

                // SlotIntervalMinutes controls how frequently slots are offered.
                // If not set, defaults to duration+buffer (one slot per booking block).
                // Set to a smaller value (e.g. 15) to offer overlapping start times.
                var stepMinutes = session.SlotIntervalMinutes ?? (session.DurationMinutes + session.BufferMinutes);

                var slot = windowStartLocal;
                while (slot.AddMinutes(session.DurationMinutes) <= windowEndLocal)
                {
                    var slotEnd = slot.AddMinutes(session.DurationMinutes);
                    // Add buffer so the next slot starts after the buffer
                    var bufferedEnd = slotEnd.AddMinutes(session.BufferMinutes);

                    // Convert to UTC for conflict checking
                    var slotUtc = TimeZoneInfo.ConvertTimeToUtc(slot, UkTimeZone);
                    var slotEndUtc = TimeZoneInfo.ConvertTimeToUtc(slotEnd, UkTimeZone);

                    // Enforce minimum notice
                    if (slotUtc < minBookableUtc)
                    {
                        slot = slot.AddMinutes(stepMinutes);
                        continue;
                    }

                    // Check if slot conflicts with any busy period.
                    // Buffer is applied on both sides: the slot is blocked if any busy period
                    // falls within [slot - buffer, slot + duration + buffer], ensuring there
                    // is breathing room before AND after any calendar event.
                    var bufferedStartUtc = TimeZoneInfo.ConvertTimeToUtc(slot.AddMinutes(-session.BufferMinutes), UkTimeZone);
                    var bufferedEndUtc = TimeZoneInfo.ConvertTimeToUtc(bufferedEnd, UkTimeZone);
                    var isBusy = busyUtcSlots.Any(b =>
                        bufferedStartUtc < b.End && bufferedEndUtc > b.Start);

                    if (!isBusy)
                        available.Add(slotUtc);

                    slot = slot.AddMinutes(stepMinutes);
                }
            }
        }

        return available.OrderBy(s => s).ToList();
    }
}
