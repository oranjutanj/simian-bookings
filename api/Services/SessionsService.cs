using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<SessionsService> _logger;

    public SessionsService(IConfiguration config, ILogger<SessionsService> logger)
    {
        _logger = logger;

        // sessions.json sits at the repo root, one level up from /api
        var basePath = config["AzureWebJobsScriptRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..");

        var sessionsPath = Path.Combine(basePath, "sessions.json");

        if (!File.Exists(sessionsPath))
            sessionsPath = Path.Combine(AppContext.BaseDirectory, "sessions.json");

        _logger.LogInformation("Loading sessions configuration from {SessionsPath}", sessionsPath);

        var json = File.ReadAllText(sessionsPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var trimmed = json.TrimStart();
        if (trimmed.StartsWith("{"))
        {
            var structuredConfig = JsonSerializer.Deserialize<SessionsConfiguration>(json, options)
                ?? throw new InvalidOperationException("sessions.json is missing required configuration.");

            _sessions = structuredConfig.SessionTypes ?? [];
            _availabilityWindows = structuredConfig.AvailabilityWindows ?? [];
            _logger.LogInformation(
                "Loaded sessions config: {SessionCount} sessions, {WindowCount} availability windows",
                _sessions.Count,
                _availabilityWindows.Count);
            return;
        }

        // Backward compatibility with the original array-only sessions.json format.
        _sessions = JsonSerializer.Deserialize<List<SessionType>>(json, options) ?? [];
        _availabilityWindows = _sessions
            .SelectMany(s => s.AvailabilityWindows ?? [])
            .ToList();
        _logger.LogInformation(
            "Loaded legacy sessions config: {SessionCount} sessions, {WindowCount} availability windows",
            _sessions.Count,
            _availabilityWindows.Count);
    }

    public List<SessionType> GetAll() => _sessions;

    public SessionType? GetById(string id) =>
        _sessions.FirstOrDefault(s => s.Id == id);

    public List<AvailabilityWindow> GetAvailabilityWindows() => _availabilityWindows;
}
