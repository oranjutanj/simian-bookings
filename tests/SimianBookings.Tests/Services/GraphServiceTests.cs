using SimianBookings.Services;

namespace SimianBookings.Tests.Services;

/// <summary>
/// Tests for GraphService.ParseGraphDateTimeToUtc.
///
/// Background: Graph API returns event start/end as a dateTime string plus a timeZone label.
/// When the label is "UTC" the value is already UTC and must NOT be converted again.
/// When the label is the mailbox timezone (e.g. "GMT Standard Time") it must be converted.
/// Failing to check the label causes a double-conversion bug: during BST (UTC+1) a UTC time
/// like 19:15 would be treated as BST and shifted to 18:15 UTC — making busy slots appear
/// an hour earlier than they really are, so genuinely blocked slots appear available.
/// </summary>
public class GraphServiceTests
{
    private static readonly TimeZoneInfo GmtStandardTime =
        TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

    // A summer datetime where BST = UTC+1, so a wrong conversion would subtract 1 hour.
    // 2026-06-11 19:15 UTC — the exact scenario that caused the reported bug.
    private static readonly DateTime SummerUtcTime =
        new(2026, 6, 11, 19, 15, 0, DateTimeKind.Unspecified);

    [Fact]
    public void ParseGraphDateTimeToUtc_WhenGraphReturnsUtcLabel_DoesNotShiftTime()
    {
        // Graph returns "UTC" as the timeZone label — value is already UTC, no conversion needed.
        var result = GraphService.ParseGraphDateTimeToUtc(SummerUtcTime, "UTC", GmtStandardTime);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(19, result.Hour); // Must stay at 19:15, not shift to 18:15
        Assert.Equal(15, result.Minute);
    }

    [Fact]
    public void ParseGraphDateTimeToUtc_WhenGraphReturnsMailboxTimezone_ConvertsCorrectly()
    {
        // Graph returns the mailbox timezone label — value is in local time and needs converting.
        // BST local 20:15 → 19:15 UTC
        var bstLocal = new DateTime(2026, 6, 11, 20, 15, 0, DateTimeKind.Unspecified);

        var result = GraphService.ParseGraphDateTimeToUtc(bstLocal, "GMT Standard Time", GmtStandardTime);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(19, result.Hour);
        Assert.Equal(15, result.Minute);
    }

    [Fact]
    public void ParseGraphDateTimeToUtc_WhenGraphTimezoneIsNull_TreatsAsMailboxTimezone()
    {
        // Null timeZone label falls back to mailbox timezone conversion.
        // BST local 20:15 → 19:15 UTC
        var bstLocal = new DateTime(2026, 6, 11, 20, 15, 0, DateTimeKind.Unspecified);

        var result = GraphService.ParseGraphDateTimeToUtc(bstLocal, null, GmtStandardTime);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(19, result.Hour);
        Assert.Equal(15, result.Minute);
    }

    [Fact]
    public void ParseGraphDateTimeToUtc_WinterTime_NoOffsetApplied()
    {
        // In winter GMT = UTC so conversion either way should produce the same result.
        var winterTime = new DateTime(2026, 1, 15, 19, 15, 0, DateTimeKind.Unspecified);

        var resultUtcLabel = GraphService.ParseGraphDateTimeToUtc(winterTime, "UTC", GmtStandardTime);
        var resultMailboxLabel = GraphService.ParseGraphDateTimeToUtc(winterTime, "GMT Standard Time", GmtStandardTime);

        Assert.Equal(19, resultUtcLabel.Hour);
        Assert.Equal(19, resultMailboxLabel.Hour); // GMT = UTC in winter, no shift
    }
}
