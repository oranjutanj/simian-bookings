using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using System.Net;
using System.Text.Json;
using SimianBookings.Models;
using SimianBookings.Services;

namespace SimianBookings.Functions;

public class GetAvailableSlots
{
    private readonly IEnumerable<ICalendarSource> _calendarSources;
    private readonly ISessionsService _sessions;
    private readonly ILogger<GetAvailableSlots> _logger;

    public GetAvailableSlots(IEnumerable<ICalendarSource> calendarSources, ISessionsService sessions, ILogger<GetAvailableSlots> logger)
    {
        _calendarSources = calendarSources;
        _sessions = sessions;
        _logger = logger;
    }

    [Function("GetAvailableSlots")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "slots")] HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var sessionTypeId = query["sessionType"];
        var weeksAheadStr = query["weeksAhead"];

        if (string.IsNullOrEmpty(sessionTypeId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            AddCorsHeaders(bad);
            await bad.WriteStringAsync("sessionType parameter is required");
            return bad;
        }

        var session = _sessions.GetById(sessionTypeId);
        if (session == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            AddCorsHeaders(notFound);
            await notFound.WriteStringAsync($"Session type '{sessionTypeId}' not found");
            return notFound;
        }

        var weeksAhead = int.TryParse(weeksAheadStr, out var w) ? w : 2;
        var fromUtc = DateTime.UtcNow;
        var toUtc = fromUtc.AddDays(weeksAhead * 7);

        try
        {
            var busyTasks = _calendarSources.Select(s => s.GetBusySlotsAsync(fromUtc, toUtc));
            var busyResults = await Task.WhenAll(busyTasks);
            var busySlots = busyResults.SelectMany(slots => slots).ToList();

            var availabilityWindows = _sessions.GetAvailabilityWindows();
            var availableSlots = SlotCalculator.GetAvailableSlots(
                session,
                availabilityWindows,
                busySlots,
                fromUtc,
                toUtc);

            var response = new SlotResponse(
                session.Id,
                session.Name,
                session.DurationMinutes,
                availableSlots.Select(s => s.ToString("o")).ToList()
            );

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json");
            AddCorsHeaders(ok);
            await ok.WriteStringAsync(JsonSerializer.Serialize(response,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            return ok;
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogError(ex, "Calendar authentication failed while fetching available slots");
            var authError = req.CreateResponse(HttpStatusCode.BadGateway);
            AddCorsHeaders(authError);
            await authError.WriteStringAsync("Calendar authentication failed. Check TenantId, ClientId, and ClientSecret in local settings.");
            return authError;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching available slots");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            AddCorsHeaders(error);
            await error.WriteStringAsync("An error occurred fetching available slots");
            return error;
        }
    }

    [Function("GetSessionTypes")]
    public async Task<HttpResponseData> GetSessionTypes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "session-types")] HttpRequestData req)
    {
        try
        {
            var all = _sessions.GetAll().Select(s => new
            {
                s.Id,
                s.Name,
                s.Description,
                s.DurationMinutes
            }).ToList();

            _logger.LogInformation("Session types requested. Returning {SessionCount} session types.", all.Count);

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json");
            AddCorsHeaders(ok);
            await ok.WriteStringAsync(JsonSerializer.Serialize(all,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading session types");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            AddCorsHeaders(error);
            await error.WriteStringAsync("An error occurred loading session types");
            return error;
        }
    }

    private static void AddCorsHeaders(HttpResponseData response)
    {
        response.Headers.Add("Access-Control-Allow-Origin", "*");
    }
}
