using JellyFish.Api.Domain.Services;
using JellyFish.Api.Common;
using Shouldly;
using Xunit;


namespace JellyFish.Api.Tests.Domain;

public class SolarTimesServiceTests
{
    private readonly ISolarTimesService _svc = new SolarTimesService();
    [Fact]
    public void SanityAgainstReference_NewYork_2025_06_15()
    {
        // Reference values from timeanddate.com (uses standard refraction),
        // consistent with NOAA/NREL algorithms.
        // New York: lat=40.709, lon=-74.011 (from page metadata), date 2025-06-15 (EDT, UTC-4)
        var date = new DateOnly(2025, 6, 15);
        double lat = 40.709, lon = -74.011;

        var res = _svc.CalculateUtc(lat, lon, 10, date);
        res.IsSuccess.ShouldBeTrue();
        var v = res.Value;

        // Expected (UTC): sunrise 09:24, solar noon ~16:56, sunset ~00:29 next day, day length ~ 15:04:43 (~905 min)
        var expectedSunriseUtc = new DateTime(2025, 6, 15, 9, 24, 0, DateTimeKind.Utc);
        var expectedSolarNoonUtc = new DateTime(2025, 6, 15, 16, 56, 0, DateTimeKind.Utc);
        var expectedSunsetUtc = new DateTime(2025, 6, 16, 0, 29, 0, DateTimeKind.Utc);
        var expectedDayLenMin = 905;

        // Allow a small tolerance (minutes)
        double tolMin = 6.0;
        v.SunriseUtc.ShouldNotBeNull();
        v.SunsetUtc.ShouldNotBeNull();
        Math.Abs((v.SunriseUtc!.Value - expectedSunriseUtc).TotalMinutes).ShouldBeLessThanOrEqualTo(tolMin);
        Math.Abs((v.SolarNoonUtc - expectedSolarNoonUtc).TotalMinutes).ShouldBeLessThanOrEqualTo(tolMin);
        Math.Abs((v.SunsetUtc!.Value - expectedSunsetUtc).TotalMinutes).ShouldBeLessThanOrEqualTo(tolMin);
        Math.Abs(v.DayLengthMinutes - expectedDayLenMin).ShouldBeLessThanOrEqualTo(6);
    }
    [Fact]
    public void SanityAgainstReference_Denver_2025_06_15()
    {
        // Reference values from timeanddate.com (standard refraction), local MDT (UTC-6)
        // Denver: lat=39.746, lon=-104.991 (from page metadata), date 2025-06-15
        var date = new DateOnly(2025, 6, 15);
        double lat = 39.746, lon = -104.991;

        // Use sea-level for reference parity; elevation would lengthen day noticeably
        var res = _svc.CalculateUtc(lat, lon, 0, date);
        res.IsSuccess.ShouldBeTrue();
        var v = res.Value;

        // Expected (UTC): sunrise 11:31, solar noon 19:00, sunset 02:29 next day, day length ~ 14:58:26 (~898 min)
        var expectedSunriseUtc = new DateTime(2025, 6, 15, 11, 31, 0, DateTimeKind.Utc);
        var expectedSolarNoonUtc = new DateTime(2025, 6, 15, 19, 0, 0, DateTimeKind.Utc);
        var expectedSunsetUtc = new DateTime(2025, 6, 16, 2, 29, 0, DateTimeKind.Utc);
        var expectedDayLenMin = 898;

        double tolMin = 6.0;
        v.SunriseUtc.ShouldNotBeNull();
        v.SunsetUtc.ShouldNotBeNull();
        Math.Abs((v.SunriseUtc!.Value - expectedSunriseUtc).TotalMinutes).ShouldBeLessThanOrEqualTo(tolMin);
        Math.Abs((v.SolarNoonUtc - expectedSolarNoonUtc).TotalMinutes).ShouldBeLessThanOrEqualTo(tolMin);
        Math.Abs((v.SunsetUtc!.Value - expectedSunsetUtc).TotalMinutes).ShouldBeLessThanOrEqualTo(tolMin);
        Math.Abs(v.DayLengthMinutes - expectedDayLenMin).ShouldBeLessThanOrEqualTo(6);
    }



    [Fact]
    public void Seattle_Summer_SaneValues_AndInternalConsistency()
    {
        // Seattle approx.
        var date = new DateOnly(2025, 6, 15);
        double lat = 47.6062, lon = -122.3321;

        var res = _svc.CalculateUtc(lat, lon, 0, date);
        res.IsSuccess.ShouldBeTrue();
        var v = res.Value;

        // Mid-latitude summer: long day
        v.DayLengthMinutes.ShouldBeGreaterThan(900);   // > 15h
        v.DayLengthMinutes.ShouldBeLessThan(1100);     // < ~18h

        // Has sunrise/sunset
        v.SunriseUtc.ShouldNotBeNull();
        v.SunsetUtc.ShouldNotBeNull();

        // Sunrise < SolarNoon < Sunset
        v.SunriseUtc!.Value.ShouldBeLessThan(v.SolarNoonUtc);
        v.SolarNoonUtc.ShouldBeLessThan(v.SunsetUtc!.Value);

        // Solar noon close to midpoint of sunrise/sunset
        var midpoint = v.SunriseUtc.Value + TimeSpan.FromMinutes((v.SunsetUtc.Value - v.SunriseUtc.Value).TotalMinutes / 2.0);
        (Math.Abs((v.SolarNoonUtc - midpoint).TotalMinutes)).ShouldBeLessThanOrEqualTo(2.0);

        // Day length equals (sunset - sunrise) within tolerance
        var dl = (int)Math.Round((v.SunsetUtc.Value - v.SunriseUtc.Value).TotalMinutes);
        Math.Abs(v.DayLengthMinutes - dl).ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public void Elevation_Increases_DayLength_AndExpandsEndpoints()
    {
        var date = new DateOnly(2025, 6, 15);
        double lat = 40.7128, lon = -74.0060; // NYC approx.

        var seaLevel = _svc.CalculateUtc(lat, lon, 0, date).Value;
        var highElev = _svc.CalculateUtc(lat, lon, 2000, date).Value; // 2km elevation

        highElev.DayLengthMinutes.ShouldBeGreaterThan(seaLevel.DayLengthMinutes);
        highElev.SunriseUtc!.Value.ShouldBeLessThan(seaLevel.SunriseUtc!.Value); // earlier sunrise
        highElev.SunsetUtc!.Value.ShouldBeGreaterThan(seaLevel.SunsetUtc!.Value); // later sunset
    }

    [Fact]
    public void Tromso_MidnightSun_HasNoSunriseOrSunset()
    {
        // Tromsø approx: 69.6492° N, 18.9553° E. Around summer solstice has midnight sun.
        var date = new DateOnly(2025, 6, 21);
        double lat = 69.6492, lon = 18.9553;

        var res = _svc.CalculateUtc(lat, lon, 0, date);
        res.IsSuccess.ShouldBeTrue();
        var v = res.Value;

        v.SunriseUtc.ShouldBeNull();
        v.SunsetUtc.ShouldBeNull();
        v.DayLengthMinutes.ShouldBe(24 * 60);
    }

    [Fact]
    public void Tromso_PolarNight_HasNoSunriseOrSunset()
    {
        var date = new DateOnly(2025, 12, 21);
        double lat = 69.6492, lon = 18.9553;

        var res = _svc.CalculateUtc(lat, lon, 0, date);
        res.IsSuccess.ShouldBeTrue();
        var v = res.Value;

        v.SunriseUtc.ShouldBeNull();
        v.SunsetUtc.ShouldBeNull();
        v.DayLengthMinutes.ShouldBe(0);
    }

    [Fact]
    public void InvalidCoordinates_ReturnFailure()
    {
        var badLat = _svc.CalculateUtc(-91, 0, 0, new DateOnly(2025,1,1));
        badLat.IsFailure.ShouldBeTrue();
        badLat.Error.Type.ShouldBe(ErrorType.Validation);

        var badLon = _svc.CalculateUtc(0, 181, 0, new DateOnly(2025,1,1));
        badLon.IsFailure.ShouldBeTrue();
        badLon.Error.Type.ShouldBe(ErrorType.Validation);
    }
}

