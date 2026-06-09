using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimianBookings.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register GraphService as a singleton, then expose it under both interfaces.
        // IEnumerable<ICalendarSource> injected into functions will receive both
        // GraphService and GoogleCalendarService.
        services.AddSingleton<GraphService>();
        services.AddSingleton<IGraphService>(sp => sp.GetRequiredService<GraphService>());
        services.AddSingleton<ICalendarSource>(sp => sp.GetRequiredService<GraphService>());
        services.AddSingleton<ICalendarSource, GoogleCalendarService>();
        services.AddSingleton<ISessionsService, SessionsService>();
    })
    .Build();

host.Run();
