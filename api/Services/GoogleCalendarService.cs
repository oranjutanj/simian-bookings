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
/// Reads busy time blocks from a personal Google Calendar using a stored OAuth 2.0 refresh token.
/// If credentials are not configured, this source is silently skipped (returns empty list).
/// </summary>
public class GoogleCalendarService : ICalendarSource
{
    private readonly CalendarService? _calendarService;
    private readonly string? _calendarId;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(IConfiguration config, ILogger<GoogleCalendarService> logger)
    {
        _logger = logger;

        var clientId = config["GoogleClientId"];
        var clientSecret = config["GoogleClientSecret"];
        var refreshToken = config["GoogleRefreshToken"];
        _calendarId = config["GoogleCalendarId"];

        if (string.IsNullOrEmpty(clientId) ||
            string.IsNullOrEmpty(clientSecret) ||
            string.IsNullOrEmpty(refreshToken) ||
            string.IsNullOrEmpty(_calendarId))
        {
            _logger.LogWarning(
                "Google Calendar credentials not fully configured. " +
                "Set GoogleClientId, GoogleClientSecret, GoogleRefreshToken, and GoogleCalendarId " +
                "to enable Google Calendar conflict checking.");
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
    /// Returns busy UTC time blocks from Google Calendar.
    /// Returns an empty list (does not throw) if the service is not configured or a transient error occurs.
    /// </summary>
    public async Task<List<(DateTime Start, DateTime End)>> GetBusySlotsAsync(DateTime fromUtc, DateTime toUtc)
    {
        if (_calendarService == null)
            return [];

        try
        {
            var fbRequest = new FreeBusyRequest
            {
                TimeMinDateTimeOffset = fromUtc,
                TimeMaxDateTimeOffset = toUtc,
                Items = [new FreeBusyRequestItem { Id = _calendarId }]
            };

            var response = await _calendarService.Freebusy.Query(fbRequest).ExecuteAsync();

            if (!response.Calendars.TryGetValue(_calendarId!, out var calendar))
                return [];

            return calendar.Busy
                .Where(b => b.StartDateTimeOffset.HasValue && b.EndDateTimeOffset.HasValue)
                .Select(b => (
                    b.StartDateTimeOffset!.Value.UtcDateTime,
                    b.EndDateTimeOffset!.Value.UtcDateTime
                ))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Google Calendar busy slots. Proceeding without Google Calendar data.");
            return [];
        }
    }
}
