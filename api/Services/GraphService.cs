using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Extensions.Configuration;
using SimianBookings.Models;

namespace SimianBookings.Services;

/// <summary>
/// Outlook-specific operations: creates calendar events with Teams meeting links.
/// Use ICalendarSource for busy-slot checking (implemented by both Graph and Google).
/// </summary>
public interface IGraphService
{
    Task<(string EventId, string TeamsLink)> CreateEventAsync(
        string subject,
        string attendeeName,
        string attendeeEmail,
        DateTime startUtc,
        DateTime endUtc,
        string description);

    Task SendBookingNotificationAsync(
        string attendeeName,
        string attendeeEmail,
        string sessionName,
        DateTime startUtc,
        string teamsLink,
        string? message);
}

public class GraphService : IGraphService, ICalendarSource
{
    private readonly GraphServiceClient _client;
    private readonly string _userId;
    private readonly string _timeZone;

    public GraphService(IConfiguration config)
    {
        var tenantId = config["TenantId"] ?? throw new InvalidOperationException("TenantId not configured");
        var clientId = config["ClientId"] ?? throw new InvalidOperationException("ClientId not configured");
        var clientSecret = config["ClientSecret"] ?? throw new InvalidOperationException("ClientSecret not configured");

        _userId = config["CalendarUserId"] ?? throw new InvalidOperationException("CalendarUserId not configured");
        _timeZone = config["CalendarTimeZone"] ?? "GMT Standard Time";

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        _client = new GraphServiceClient(credential);
    }

    /// <summary>
    /// Returns busy time blocks from Outlook calendar for the given UTC range.
    /// </summary>
    public async Task<List<(DateTime Start, DateTime End)>> GetBusySlotsAsync(DateTime fromUtc, DateTime toUtc)
    {
        var events = await _client.Users[_userId].CalendarView.GetAsync(req =>
        {
            req.QueryParameters.StartDateTime = fromUtc.ToString("o");
            req.QueryParameters.EndDateTime = toUtc.ToString("o");
            req.QueryParameters.Select = ["start", "end", "showAs", "isCancelled"];
            req.QueryParameters.Top = 100;
        });

        var busy = new List<(DateTime Start, DateTime End)>();

        var page = events;
        while (page?.Value != null)
        {
            foreach (var ev in page.Value)
            {
                // Skip cancelled, free, or working-elsewhere events
                if (ev.IsCancelled == true) continue;
                if (ev.ShowAs == FreeBusyStatus.Free) continue;
                if (ev.ShowAs == FreeBusyStatus.WorkingElsewhere) continue;

                if (DateTime.TryParse(ev.Start?.DateTime, out var start) &&
                    DateTime.TryParse(ev.End?.DateTime, out var end))
                {
                    // Graph returns times in the user's mailbox timezone - convert to UTC
                    var tz = TimeZoneInfo.FindSystemTimeZoneById(_timeZone);
                    busy.Add((
                        TimeZoneInfo.ConvertTimeToUtc(start, tz),
                        TimeZoneInfo.ConvertTimeToUtc(end, tz)
                    ));
                }
            }

            // Handle pagination
            if (page.OdataNextLink == null) break;
            page = await _client.Users[_userId].CalendarView
                .WithUrl(page.OdataNextLink)
                .GetAsync();
        }

        return busy;
    }

    /// <summary>
    /// Creates a calendar event with a Teams meeting link and returns the event ID and join URL.
    /// </summary>
    public async Task<(string EventId, string TeamsLink)> CreateEventAsync(
        string subject,
        string attendeeName,
        string attendeeEmail,
        DateTime startUtc,
        DateTime endUtc,
        string description)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_timeZone);
        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(startUtc, tz);
        var endLocal = TimeZoneInfo.ConvertTimeFromUtc(endUtc, tz);

        var newEvent = new Event
        {
            Subject = subject,
            Body = new ItemBody
            {
                ContentType = BodyType.Html,
                Content = $"<p>{description}</p>"
            },
            Start = new DateTimeTimeZone
            {
                DateTime = startLocal.ToString("yyyy-MM-ddTHH:mm:ss"),
                TimeZone = _timeZone
            },
            End = new DateTimeTimeZone
            {
                DateTime = endLocal.ToString("yyyy-MM-ddTHH:mm:ss"),
                TimeZone = _timeZone
            },
            IsOnlineMeeting = true,
            OnlineMeetingProvider = OnlineMeetingProviderType.TeamsForBusiness,
            IsReminderOn = true,
            ReminderMinutesBeforeStart = 60,
            Attendees =
            [
                new Attendee
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = attendeeEmail,
                        Name = attendeeName
                    },
                    Type = AttendeeType.Required
                }
            ]
        };

        var created = await _client.Users[_userId].Events.PostAsync(newEvent);
        var teamsLink = created?.OnlineMeeting?.JoinUrl ?? string.Empty;

        return (created?.Id ?? string.Empty, teamsLink);
    }

    /// <summary>
    /// Sends a booking notification email to the organizer (CalendarUserId).
    /// </summary>
    public async Task SendBookingNotificationAsync(
        string attendeeName,
        string attendeeEmail,
        string sessionName,
        DateTime startUtc,
        string teamsLink,
        string? message)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_timeZone);
        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(startUtc, tz);
        var friendlyTime = startLocal.ToString("dddd d MMMM yyyy 'at' h:mmtt");

        var messageSection = string.IsNullOrWhiteSpace(message)
            ? string.Empty
            : $"<p><strong>Their message:</strong><br/>{System.Web.HttpUtility.HtmlEncode(message)}</p>";

        var htmlBody = $"""
            <p>A new session has been booked through Simian Coaching.</p>
            <table cellpadding="6" style="border-collapse:collapse;font-family:sans-serif;">
              <tr><td><strong>Session</strong></td><td>{sessionName}</td></tr>
              <tr><td><strong>When</strong></td><td>{friendlyTime}</td></tr>
              <tr><td><strong>Who</strong></td><td>{System.Web.HttpUtility.HtmlEncode(attendeeName)} ({System.Web.HttpUtility.HtmlEncode(attendeeEmail)})</td></tr>
              <tr><td><strong>Teams link</strong></td><td><a href="{teamsLink}">{teamsLink}</a></td></tr>
            </table>
            {messageSection}
            """;

        var mail = new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
        {
            Message = new Message
            {
                Subject = $"New Booking: {sessionName} with {attendeeName}",
                Body = new ItemBody { ContentType = BodyType.Html, Content = htmlBody },
                ToRecipients =
                [
                    new Recipient
                    {
                        EmailAddress = new EmailAddress { Address = _userId }
                    }
                ]
            },
            SaveToSentItems = false
        };

        await _client.Users[_userId].SendMail.PostAsync(mail);
    }
}
