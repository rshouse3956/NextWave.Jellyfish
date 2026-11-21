using JellyFish.Api.Features.ActiveZone.Commands;
using Shouldly;

namespace JellyFish.Api.Tests.Features.ActiveZone.Commands;

public class StopActiveZoneCommandValidatorTests
{
    [Fact]
    public void ZoneName_required()
    {
        var v = new StopActiveZoneCommandValidator();
        v.Validate(new StopActiveZoneCommand("")).IsValid.ShouldBeFalse();
        v.Validate(new StopActiveZoneCommand("   ")).IsValid.ShouldBeFalse();
        v.Validate(new StopActiveZoneCommand("Living Room")).IsValid.ShouldBeTrue();
    }
}

