namespace SimianBookings.Models;

public record SessionType(
    string Id,
    string Name,
    string Description,
    int DurationMinutes,
    int BufferMinutes,
    List<AvailabilityWindow>? AvailabilityWindows = null
);

public record SessionsConfiguration(
    List<SessionType> SessionTypes,
    List<AvailabilityWindow> AvailabilityWindows
);

public record AvailabilityWindow(
    List<string> DaysOfWeek,
    string StartTime,  // "18:00" in local UK time
    string EndTime     // "21:00" in local UK time
);
