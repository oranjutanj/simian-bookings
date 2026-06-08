using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimianBookings.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddSingleton<IGraphService, GraphService>();
        services.AddSingleton<ISessionsService, SessionsService>();
    })
    .Build();

host.Run();
