namespace SimianBookings.Models;

public record BookingRequest(
    string SessionTypeId,
    string StartTimeUtc,   // ISO 8601 UTC
    string AttendeeName,
    string AttendeeEmail,
    string? Message
);

public record BookingResponse(
    bool Success,
    string? EventId,
    string? TeamsLink,
    string? Error
);

public record SlotResponse(
    string SessionTypeId,
    string SessionName,
    int DurationMinutes,
    List<string> AvailableSlots  // ISO 8601 UTC strings
);
