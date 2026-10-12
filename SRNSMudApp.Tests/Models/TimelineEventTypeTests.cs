using SRNSMudApp.Data;
using SRNSMudApp.Models;

namespace SRNSMudApp.Tests.Models;

public class TimelineEventTypeTests
{
    [Theory]
    [InlineData(TimelineEventType.Insert, TimelineEventTypeExtensions.InsertValue)]
    [InlineData(TimelineEventType.Update, TimelineEventTypeExtensions.UpdateValue)]
    [InlineData(TimelineEventType.Delete, TimelineEventTypeExtensions.DeleteValue)]
    public void ToDbString_ReturnsExpectedString(TimelineEventType type, string expected)
    {
        var actual = type.ToDbString();
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(TimelineEventTypeExtensions.InsertValue, TimelineEventType.Insert)]
    [InlineData(TimelineEventTypeExtensions.UpdateValue, TimelineEventType.Update)]
    [InlineData(TimelineEventTypeExtensions.DeleteValue, TimelineEventType.Delete)]
    public void ToTimelineEventType_ReturnsExpectedEnum(string value, TimelineEventType expected)
    {
        TimelineEventType? actual = value.ToTimelineEventType();
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Unknown")]
    [InlineData("insert")] // case-sensitive check
    public void ToTimelineEventType_ReturnsNull_ForInvalidValues(string? value)
    {
        TimelineEventType? actual = value.ToTimelineEventType();
        Assert.Null(actual);
    }

    [Fact]
    public void TimelineEvent_ParsedEventType_SyncsWithEventTypeProperty()
    {
        var ev = new TimelineEvent { OwnerId = "test-user" };

        ev.ParsedEventType = TimelineEventType.Insert;
        Assert.Equal("Insert", ev.EventType);
        Assert.Equal(TimelineEventType.Insert, ev.ParsedEventType);

        ev.ParsedEventType = TimelineEventType.Delete;
        Assert.Equal("Delete", ev.EventType);
        Assert.Equal(TimelineEventType.Delete, ev.ParsedEventType);

        ev.ParsedEventType = TimelineEventType.Update;
        Assert.Equal("Update", ev.EventType);
        Assert.Equal(TimelineEventType.Update, ev.ParsedEventType);

        ev.EventType = "Delete";
        Assert.Equal(TimelineEventType.Delete, ev.ParsedEventType);

        ev.EventType = "Invalid";
        Assert.Null(ev.ParsedEventType);
    }
}