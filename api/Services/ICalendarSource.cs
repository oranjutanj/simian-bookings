namespace SimianBookings.Services;

/// <summary>
/// Represents any calendar that can report busy time blocks.
/// Both Outlook (via Graph) and Google Calendar implement this.
/// </summary>
public interface ICalendarSource
{
    /// <summary>
    /// Returns busy UTC time blocks within the given range.
    /// Implementations should return an empty list (not throw) if the source is unavailable.
    /// </summary>
    Task<List<(DateTime Start, DateTime End)>> GetBusySlotsAsync(DateTime fromUtc, DateTime toUtc);
}
