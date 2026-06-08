using Microsoft.Extensions.Configuration;
using SimianBookings.Services;

namespace SimianBookings.Tests.Services;

public class SessionsServiceTests
{
    [Fact]
    public void LoadsStructuredSessionsConfigurationFromConfiguredScriptRoot()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"simian-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var json = """
                       {
                         "availabilityWindows": [
                           {
                             "daysOfWeek": ["Monday"],
                             "startTime": "18:00",
                             "endTime": "21:00"
                           }
                         ],
                         "sessionTypes": [
                           {
                             "id": "coaching-45",
                             "name": "Coaching 45",
                             "description": "desc",
                             "durationMinutes": 45,
                             "bufferMinutes": 15
                           }
                         ]
                       }
                       """;

            File.WriteAllText(Path.Combine(tempDir, "sessions.json"), json);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AzureWebJobsScriptRoot"] = tempDir
                })
                .Build();

            var service = new SessionsService(config);

            var all = service.GetAll();
            Assert.Single(all);
            Assert.Equal("coaching-45", all[0].Id);
            Assert.Single(service.GetAvailabilityWindows());
            Assert.NotNull(service.GetById("coaching-45"));
            Assert.Null(service.GetById("missing"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadsLegacyArraySessionsConfigurationForBackwardCompatibility()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"simian-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var json = """
                       [
                         {
                           "id": "coaching-45",
                           "name": "Coaching 45",
                           "description": "desc",
                           "durationMinutes": 45,
                           "bufferMinutes": 15,
                           "availabilityWindows": [
                             {
                               "daysOfWeek": ["Monday"],
                               "startTime": "18:00",
                               "endTime": "21:00"
                             }
                           ]
                         }
                       ]
                       """;

            File.WriteAllText(Path.Combine(tempDir, "sessions.json"), json);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AzureWebJobsScriptRoot"] = tempDir
                })
                .Build();

            var service = new SessionsService(config);

            Assert.Single(service.GetAll());
            Assert.Single(service.GetAvailabilityWindows());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
