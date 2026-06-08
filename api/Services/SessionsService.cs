using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SimianBookings.Models;

namespace SimianBookings.Services;

public interface ISessionsService
{
    List<SessionType> GetAll();

    SessionType? GetById(string id);
}

public class SessionsService : ISessionsService
{
    private readonly List<SessionType> _sessions;

    public SessionsService(IConfiguration config)
    {
        // sessions.json sits at the repo root, one level up from /api
        var basePath = config["AzureWebJobsScriptRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..");

        var sessionsPath = Path.Combine(basePath, "sessions.json");

        if (!File.Exists(sessionsPath))
            sessionsPath = Path.Combine(AppContext.BaseDirectory, "sessions.json");

        var json = File.ReadAllText(sessionsPath);
        _sessions = JsonSerializer.Deserialize<List<SessionType>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? [];
    }

    public List<SessionType> GetAll() => _sessions;

    public SessionType? GetById(string id) =>
        _sessions.FirstOrDefault(s => s.Id == id);
}
