using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SimianBookings.Models;

namespace SimianBookings.Services;

public interface ISessionsService
{
    List<SessionType> GetAll();

    SessionType? GetById(string id);

    List<AvailabilityWindow> GetAvailabilityWindows();
}

public class SessionsService : ISessionsService
{
    private readonly List<SessionType> _sessions;
    private readonly List<AvailabilityWindow> _availabilityWindows;

    public SessionsService(IConfiguration config)
    {
        // sessions.json sits at the repo root, one level up from /api
        var basePath = config["AzureWebJobsScriptRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..");

        var sessionsPath = Path.Combine(basePath, "sessions.json");

        if (!File.Exists(sessionsPath))
            sessionsPath = Path.Combine(AppContext.BaseDirectory, "sessions.json");

        var json = File.ReadAllText(sessionsPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var trimmed = json.TrimStart();
        if (trimmed.StartsWith("{"))
        {
            var structuredConfig = JsonSerializer.Deserialize<SessionsConfiguration>(json, options)
                ?? throw new InvalidOperationException("sessions.json is missing required configuration.");

            _sessions = structuredConfig.SessionTypes ?? [];
            _availabilityWindows = structuredConfig.AvailabilityWindows ?? [];
            return;
        }

        // Backward compatibility with the original array-only sessions.json format.
        _sessions = JsonSerializer.Deserialize<List<SessionType>>(json, options) ?? [];
        _availabilityWindows = _sessions
            .SelectMany(s => s.AvailabilityWindows ?? [])
            .ToList();
    }

    public List<SessionType> GetAll() => _sessions;

    public SessionType? GetById(string id) =>
        _sessions.FirstOrDefault(s => s.Id == id);

    public List<AvailabilityWindow> GetAvailabilityWindows() => _availabilityWindows;
}
