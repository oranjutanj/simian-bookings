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

public class CreateBookingTests
{
    [Fact]
    public async Task ReturnsBadRequest_WhenBodyInvalidJson()
    {
        var calendarSource = new Mock<ICalendarSource>(MockBehavior.Strict);
        var graph = new Mock<IGraphService>(MockBehavior.Strict);
        var sessions = new Mock<ISessionsService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<CreateBooking>>();
        var sut = new CreateBooking([calendarSource.Object], graph.Object, sessions.Object, logger.Object);

        var request = NewRequest("POST", "{invalid-json");

        var response = await sut.Run(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal("Invalid request body", body);
    }

    [Fact]
    public async Task ReturnsConflict_WhenSlotNoLongerAvailable()
    {
        var session = new SessionType(
            "coaching-45",
            "Coaching",
            "desc",
            45,
            15,
            [new AvailabilityWindow(["Monday"], "18:00", "21:00")]);

        var startUtc = new DateTime(2030, 1, 7, 19, 0, 0, DateTimeKind.Utc);
        var bookingJson = $$"""
            {
              "sessionTypeId": "coaching-45",
              "startTimeUtc": "{{startUtc:o}}",
              "attendeeName": "Test User",
              "attendeeEmail": "test@example.com",
              "message": "Hi"
            }
            """;

        var calendarSource = new Mock<ICalendarSource>(MockBehavior.Strict);
        calendarSource
            .Setup(g => g.GetBusySlotsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([
                (startUtc.AddMinutes(-5), startUtc.AddMinutes(10))
            ]);

        var graph = new Mock<IGraphService>(MockBehavior.Strict);
        var sessions = new Mock<ISessionsService>(MockBehavior.Strict);
        sessions.Setup(s => s.GetById("coaching-45")).Returns(session);

        var logger = new Mock<ILogger<CreateBooking>>();
        var sut = new CreateBooking([calendarSource.Object], graph.Object, sessions.Object, logger.Object);

        var request = NewRequest("POST", bookingJson);

        var response = await sut.Run(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Contains("no longer available", body);

        graph.Verify(g => g.CreateEventAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReturnsSuccess_WhenBookingCreated()
    {
        var session = new SessionType(
            "coaching-60",
            "Coaching",
            "desc",
            60,
            15,
            [new AvailabilityWindow(["Monday"], "18:00", "21:00")]);

        var startUtc = new DateTime(2030, 1, 7, 20, 0, 0, DateTimeKind.Utc);
        var bookingJson = $$"""
            {
              "sessionTypeId": "coaching-60",
              "startTimeUtc": "{{startUtc:o}}",
              "attendeeName": "Test User",
              "attendeeEmail": "test@example.com",
              "message": "Looking forward to it"
            }
            """;

        var calendarSource = new Mock<ICalendarSource>(MockBehavior.Strict);
        calendarSource
            .Setup(g => g.GetBusySlotsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        var graph = new Mock<IGraphService>(MockBehavior.Strict);
        graph
            .Setup(g => g.CreateEventAsync(
                It.IsAny<string>(),
                "Test User",
                "test@example.com",
                startUtc,
                startUtc.AddMinutes(60),
                It.IsAny<string>()))
            .ReturnsAsync(("event-123", "https://teams.example/join"));

        var sessions = new Mock<ISessionsService>(MockBehavior.Strict);
        sessions.Setup(s => s.GetById("coaching-60")).Returns(session);

        var logger = new Mock<ILogger<CreateBooking>>();
        var sut = new CreateBooking([calendarSource.Object], graph.Object, sessions.Object, logger.Object);

        var request = NewRequest("POST", bookingJson);

        var response = await sut.Run(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Contains("\"success\":true", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("event-123", body);
        Assert.Contains("teams.example", body);
    }

    private static FakeHttpRequestData NewRequest(string method, string body)
    {
        var context = new Mock<FunctionContext>().Object;
        return new FakeHttpRequestData(context, method, new Uri("http://localhost/api/bookings"), body);
    }

    private static async Task<string> ReadBodyAsync(Microsoft.Azure.Functions.Worker.Http.HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
