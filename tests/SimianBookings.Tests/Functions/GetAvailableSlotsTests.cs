using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using SimianBookings.Functions;
using SimianBookings.Models;
using SimianBookings.Services;
using SimianBookings.Tests.TestDoubles;

namespace SimianBookings.Tests.Functions;

public class GetAvailableSlotsTests
{
    [Fact]
    public async Task ReturnsBadRequest_WhenSessionTypeMissing()
    {
        var calendarSource = new Mock<ICalendarSource>(MockBehavior.Strict);
        var sessions = new Mock<ISessionsService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<GetAvailableSlots>>();
        var sut = new GetAvailableSlots([calendarSource.Object], sessions.Object, logger.Object);

        var request = NewRequest("http://localhost/api/slots");

        var response = await sut.Run(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReturnsNotFound_WhenSessionTypeUnknown()
    {
        var calendarSource = new Mock<ICalendarSource>(MockBehavior.Strict);
        var sessions = new Mock<ISessionsService>(MockBehavior.Strict);
        sessions.Setup(s => s.GetById("missing")).Returns((SessionType?)null);

        var logger = new Mock<ILogger<GetAvailableSlots>>();
        var sut = new GetAvailableSlots([calendarSource.Object], sessions.Object, logger.Object);

        var request = NewRequest("http://localhost/api/slots?sessionType=missing");

        var response = await sut.Run(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReturnsSlotPayload_WhenSessionExists()
    {
        var session = new SessionType(
            "coaching-45",
            "Coaching",
            "desc",
            45,
            15,
            [new AvailabilityWindow(["Monday", "Tuesday", "Wednesday", "Thursday"], "18:00", "21:00")]);

        var calendarSource = new Mock<ICalendarSource>(MockBehavior.Strict);
        calendarSource.Setup(g => g.GetBusySlotsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);

        var sessions = new Mock<ISessionsService>(MockBehavior.Strict);
        sessions.Setup(s => s.GetById("coaching-45")).Returns(session);
        sessions.Setup(s => s.GetAvailabilityWindows()).Returns(
        [
            new AvailabilityWindow(["Monday", "Tuesday", "Wednesday", "Thursday"], "18:00", "21:00")
        ]);

        var logger = new Mock<ILogger<GetAvailableSlots>>();
        var sut = new GetAvailableSlots([calendarSource.Object], sessions.Object, logger.Object);

        var request = NewRequest("http://localhost/api/slots?sessionType=coaching-45&weeksAhead=1");

        var response = await sut.Run(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Contains("coaching-45", body);
        Assert.Contains("availableSlots", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSessionTypes_ReturnsAllConfiguredSessions()
    {
        var calendarSource = new Mock<ICalendarSource>(MockBehavior.Strict);
        var sessions = new Mock<ISessionsService>(MockBehavior.Strict);
        sessions.Setup(s => s.GetAll()).Returns(
        [
            new SessionType("coaching-45", "Coaching 45", "desc", 45, 15, []),
            new SessionType("coaching-60", "Coaching 60", "desc", 60, 15, [])
        ]);

        var logger = new Mock<ILogger<GetAvailableSlots>>();
        var sut = new GetAvailableSlots([calendarSource.Object], sessions.Object, logger.Object);

        var request = NewRequest("http://localhost/api/session-types");

        var response = await sut.GetSessionTypes(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Contains("coaching-45", body);
        Assert.Contains("coaching-60", body);
    }

    private static FakeHttpRequestData NewRequest(string url)
    {
        var context = new Mock<FunctionContext>().Object;
        return new FakeHttpRequestData(context, "GET", new Uri(url));
    }

    private static async Task<string> ReadBodyAsync(Microsoft.Azure.Functions.Worker.Http.HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
