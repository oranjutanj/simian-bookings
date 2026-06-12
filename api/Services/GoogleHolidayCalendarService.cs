using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SimianBookings.Services;

/// <summary>
/// Blocks entire days that contain UK public holidays by reading events from a Google Calendar
/// (e.g. "Holidays in United Kingdom" — calendar ID en.uk#holiday@group.v.calendar.google.com).
/// Holiday events are all-day and not marked as "busy", so they are invisible to FreeBusy queries.
/// This service queries events directly and returns each holiday day as a full-day busy block.
/// </summary>
public class GoogleHolidayCalendarService : ICalendarSource
{
    private static readonly TimeZoneInfo UkTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

    private readonly CalendarService? _calendarService;
    private readonly string? _holidayCalendarId;
    private readonly ILogger<GoogleHolidayCalendarService> _logger;

    public GoogleHolidayCalendarService(IConfiguration config, ILogger<GoogleHolidayCalendarService> logger)
    {
        _logger = logger;

        var clientId = config["GoogleClientId"];
        var clientSecret = config["GoogleClientSecret"];
        var refreshToken = config["GoogleRefreshToken"];
        _holidayCalendarId = config["GoogleHolidayCalendarId"];

        if (string.IsNullOrEmpty(clientId) ||
            string.IsNullOrEmpty(clientSecret) ||
            string.IsNullOrEmpty(refreshToken) ||
            string.IsNullOrEmpty(_holidayCalendarId))
        {
            _logger.LogWarning(
                "Google Holiday Calendar not configured. " +
                "Set GoogleClientId, GoogleClientSecret, GoogleRefreshToken, and GoogleHolidayCalendarId " +
                "to enable public holiday blocking. " +
                "Typical value: en.uk#holiday@group.v.calendar.google.com");
            return;
        }

        var credential = new UserCredential(
            new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                },
                Scopes = [CalendarService.Scope.CalendarReadonly]
            }),
            "user",
            new TokenResponse { RefreshToken = refreshToken }
        );

        _calendarService = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Simian Bookings"
        });
    }

    /// <summary>
    /// Returns a full-day UTC busy block for each public holiday in the given range.
    /// Returns an empty list (does not throw) if the service is not configured or a transient error occurs.
    /// </summary>
    public async Task<List<(DateTime Start, DateTime End)>> GetBusySlotsAsync(DateTime fromUtc, DateTime toUtc)
    {
        if (_calendarService == null)
            return [];

        try
        {
            var request = _calendarService.Events.List(_holidayCalendarId);
            request.TimeMinDateTimeOffset = fromUtc;
            request.TimeMaxDateTimeOffset = toUtc;
            request.SingleEvents = true;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            var events = await request.ExecuteAsync();

            var busyDays = new List<(DateTime Start, DateTime End)>();

            foreach (var ev in events.Items ?? [])
            {
                // All-day events have a Date string rather than a DateTime
                if (ev.Start?.Date != null && DateOnly.TryParse(ev.Start.Date, out var holidayDate))
                {
                    // Block the entire UK calendar day, converted to UTC
                    var dayStartLocal = holidayDate.ToDateTime(TimeOnly.MinValue);
                    var dayEndLocal = dayStartLocal.AddDays(1);
                    var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal, UkTimeZone);
                    var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(dayEndLocal, UkTimeZone);

                    busyDays.Add((dayStartUtc, dayEndUtc));
                    _logger.LogInformation("Blocking public holiday: {Name} on {Date}", ev.Summary, holidayDate);
                }
            }

            return busyDays;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Google Holiday Calendar events. Proceeding without holiday blocking.");
            return [];
        }
    }
}
