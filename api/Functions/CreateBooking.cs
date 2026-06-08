using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using SimianBookings.Models;
using SimianBookings.Services;

namespace SimianBookings.Functions;

public class CreateBooking
{
    private readonly IGraphService _graph;
    private readonly ISessionsService _sessions;
    private readonly ILogger<CreateBooking> _logger;

    public CreateBooking(IGraphService graph, ISessionsService sessions, ILogger<CreateBooking> logger)
    {
        _graph = graph;
        _sessions = sessions;
        _logger = logger;
    }

    [Function("CreateBooking")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "bookings")] HttpRequestData req)
    {
        // Handle CORS preflight
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var preflight = req.CreateResponse(HttpStatusCode.NoContent);
            AddCorsHeaders(preflight);
            return preflight;
        }

        BookingRequest? booking;
        try
        {
            var body = await req.ReadAsStringAsync();
            booking = JsonSerializer.Deserialize<BookingRequest>(body ?? "",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid request body");
            return bad;
        }

        if (booking == null ||
            string.IsNullOrEmpty(booking.SessionTypeId) ||
            string.IsNullOrEmpty(booking.StartTimeUtc) ||
            string.IsNullOrEmpty(booking.AttendeeName) ||
            string.IsNullOrEmpty(booking.AttendeeEmail))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing required fields: sessionTypeId, startTimeUtc, attendeeName, attendeeEmail");
            return bad;
        }

        var session = _sessions.GetById(booking.SessionTypeId);
        if (session == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync($"Session type '{booking.SessionTypeId}' not found");
            return notFound;
        }

        if (!DateTime.TryParse(booking.StartTimeUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var startUtc))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid startTimeUtc format. Use ISO 8601.");
            return bad;
        }

        var endUtc = startUtc.AddMinutes(session.DurationMinutes);

        try
        {
            // Double-check availability before creating
            var busySlots = await _graph.GetBusySlotsAsync(
                startUtc.AddMinutes(-1),
                endUtc.AddMinutes(session.BufferMinutes + 1));

            var isConflict = busySlots.Any(b => startUtc < b.End && endUtc > b.Start);
            if (isConflict)
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                AddCorsHeaders(conflict);
                await conflict.WriteStringAsync(JsonSerializer.Serialize(
                    new BookingResponse(false, null, null, "That slot is no longer available. Please choose another time."),
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                return conflict;
            }

            var subject = $"Simian Coaching: {session.Name} with {booking.AttendeeName}";
            var description = string.IsNullOrEmpty(booking.Message)
                ? $"Thank you for booking a {session.Name} session with Simian Coaching."
                : $"Thank you for booking a {session.Name} session with Simian Coaching.<br/><br/><strong>Your message:</strong> {booking.Message}";

            var (eventId, teamsLink) = await _graph.CreateEventAsync(
                subject,
                booking.AttendeeName,
                booking.AttendeeEmail,
                startUtc,
                endUtc,
                description);

            var ok = req.CreateResponse(HttpStatusCode.OK);
            AddCorsHeaders(ok);
            ok.Headers.Add("Content-Type", "application/json");
            await ok.WriteStringAsync(JsonSerializer.Serialize(
                new BookingResponse(true, eventId, teamsLink, null),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            AddCorsHeaders(error);
            await error.WriteStringAsync(JsonSerializer.Serialize(
                new BookingResponse(false, null, null, "An unexpected error occurred. Please try again."),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            return error;
        }
    }

    private static void AddCorsHeaders(HttpResponseData response)
    {
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
    }
}
