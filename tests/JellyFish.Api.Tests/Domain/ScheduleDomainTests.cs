using JellyFish.Api.Domain;
using Shouldly;
using System.Collections.Generic;

namespace JellyFish.Api.Tests.Domain;

public class ScheduleDomainTests
{
    [Fact]
    public void Create_Should_Succeed_With_Valid_Values()
    {
        var res = Schedule.Create(
            name: "MorningOn",
            folderName: "Home",
            patternName: "Sunrise",
            priority: "High",
            onTime: "07:00",
            onDay: "* * *",
            offTime: "08:00",
            offDay: "* * *",
            zoneNames: new List<string>{"Z1"},
            nextEventUtc: DateTime.UtcNow.AddMinutes(5));

        res.IsSuccess.ShouldBeTrue();
        res.Value.Name.ShouldBe("MorningOn");
        res.Value.Priority.ShouldBe("High");
        res.Value.OnTime.ShouldBe("07:00");
        res.Value.OffTime.ShouldBe("08:00");
        res.Value.OnDay.ShouldBe("* * *");
        res.Value.OffDay.ShouldBe("* * *");
        res.Value.NextEvent.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_Should_Fail_When_Name_Missing(string? name)
    {
        var res = Schedule.Create(name!, "Home", "Sunrise", "Normal", "07:00", "* * *", "08:00", "* * *", new List<string>{"Z1"}, null);
        res.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Fail_When_Priority_Invalid()
    {
        var res = Schedule.Create("X", "Home", "Sunrise", "Urgent", "07:00", "* * *", "08:00", "* * *", new List<string>{"Z1"}, null);
        res.IsFailure.ShouldBeTrue();
    }
}

