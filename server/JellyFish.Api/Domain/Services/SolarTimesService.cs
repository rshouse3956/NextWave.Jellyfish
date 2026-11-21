using JellyFish.Api.Common;

namespace JellyFish.Api.Domain.Services;

/// <summary>
/// Lightweight sunrise/sunset calculator using equation-of-time + declination method
/// with standard refraction (-0.833°) and elevation (h) adjustment via geometric horizon dip.
/// Accuracy: ~1–2 minutes for typical latitudes; elevation modeled geometrically only.
/// All times returned in UTC.
/// </summary>
public sealed class SolarTimesService : ISolarTimesService
{
    private const double SolarZenithSeaLevelDeg = 90.833; // refraction + solar radius
    private const double EarthRadiusMeters = 6371000.0;

    public Result<SolarTimes> CalculateUtc(double latitudeDeg, double longitudeDeg, double elevationMeters, DateOnly date)
    {
        if (latitudeDeg < -90 || latitudeDeg > 90)
            return Result.Failure<SolarTimes>(Error.Validation("Latitude must be between -90 and 90 degrees."));
        if (longitudeDeg < -180 || longitudeDeg > 180)
            return Result.Failure<SolarTimes>(Error.Validation("Longitude must be between -180 and 180 degrees."));

        // Day of year (1..366)
        int N = date.DayOfYear;

        // Fractional year (radians) — at 12:00 (noon) for stability (doesn't materially affect EoT/decl)
        double gamma = 2.0 * Math.PI / 365.0 * (N - 1);

        // Equation of time (minutes)
        double eotMinutes = 229.18 * (
            0.000075
            + 0.001868 * Math.Cos(gamma)
            - 0.032077 * Math.Sin(gamma)
            - 0.014615 * Math.Cos(2 * gamma)
            - 0.040849 * Math.Sin(2 * gamma)
        );

        // Solar declination (radians)
        double decl =
            0.006918
            - 0.399912 * Math.Cos(gamma)
            + 0.070257 * Math.Sin(gamma)
            - 0.006758 * Math.Cos(2 * gamma)
            + 0.000907 * Math.Sin(2 * gamma)
            - 0.002697 * Math.Cos(3 * gamma)
            + 0.00148 * Math.Sin(3 * gamma);

        double latRad = Deg2Rad(latitudeDeg);

        // Elevation adjustment: visible horizon dip (degrees). Negative elevations clamped to 0.
        double h = Math.Max(0.0, elevationMeters);
        double dipRad = Math.Sqrt(2.0 * h / EarthRadiusMeters); // small-angle approx of acos(R/(R+h))
        double dipDeg = Rad2Deg(dipRad);

        double zenithDeg = SolarZenithSeaLevelDeg + dipDeg; // higher zenith => earlier sunrise/later sunset
        double cosZ = Math.Cos(Deg2Rad(zenithDeg));

        double cosH = (cosZ - Math.Sin(latRad) * Math.Sin(decl)) / (Math.Cos(latRad) * Math.Cos(decl));

        if (double.IsNaN(cosH))
            return Result.Failure<SolarTimes>(Error.Problem("Numerical error while computing hour angle."));

        if (cosH > 1.0)
        {
            // Polar night — sun never rises on this date at this latitude
            double solarNoonMinutesUtcPolar = 720.0 - 4.0 * longitudeDeg - eotMinutes;
            var dayStartUtc_polar = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            DateTime solarNoonUtc_polar = dayStartUtc_polar.AddMinutes(solarNoonMinutesUtcPolar);
            return Result.Success(new SolarTimes(null, null, solarNoonUtc_polar, 0));
        }
        if (cosH < -1.0)
        {
            // Midnight sun — sun never sets on this date at this latitude
            double solarNoonMinutesUtcPolar = 720.0 - 4.0 * longitudeDeg - eotMinutes;
            var dayStartUtc_polar = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            DateTime solarNoonUtc_polar = dayStartUtc_polar.AddMinutes(solarNoonMinutesUtcPolar);
            return Result.Success(new SolarTimes(null, null, solarNoonUtc_polar, 24 * 60));
        }

        double H0deg = Rad2Deg(Math.Acos(cosH));

        // Solar noon in minutes from 00:00 UTC
        // 720 min = 12h; 4*longitude converts degrees to minutes (positive east)
        double solarNoonMinutesUtc = 720.0 - 4.0 * longitudeDeg - eotMinutes;

        double deltaMinutes = 4.0 * H0deg; // 1 degree of hour angle = 4 minutes

        double sunriseMinutes = solarNoonMinutesUtc - deltaMinutes;
        double sunsetMinutes  = solarNoonMinutesUtc + deltaMinutes;

        var dayStartUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        DateTime sunriseUtc = dayStartUtc.AddMinutes(sunriseMinutes);
        DateTime sunsetUtc  = dayStartUtc.AddMinutes(sunsetMinutes);
        int dayLengthMinutes = (int)Math.Round(2.0 * deltaMinutes);
        DateTime solarNoonUtc = dayStartUtc.AddMinutes(solarNoonMinutesUtc);

        return Result.Success(new SolarTimes(sunriseUtc, sunsetUtc, solarNoonUtc, dayLengthMinutes));
    }

    private static double Deg2Rad(double deg) => deg * Math.PI / 180.0;
    private static double Rad2Deg(double rad) => rad * 180.0 / Math.PI;
}

