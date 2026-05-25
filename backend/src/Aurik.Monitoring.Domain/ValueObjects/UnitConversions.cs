namespace Aurik.Monitoring.Domain.ValueObjects;

/// <summary>
/// Centralized, deterministic unit conversions used across normalizers.
/// Kept as a static class so behavior is consistent and easily unit-testable.
/// </summary>
public static class UnitConversions
{
    /// <summary>°F → °C</summary>
    public static double FahrenheitToCelsius(double fahrenheit) =>
        Math.Round((fahrenheit - 32d) * 5d / 9d, 4);

    /// <summary>
    /// g (gravitational acceleration) → mm/s vibration velocity at ~10 Hz reference frequency.
    /// Assumption documented in README — vendor does not provide frequency. v(mm/s) = g * 9806.65 / (2*pi*f)
    /// </summary>
    public static double GravityToMmPerSec(double gValue, double referenceHzFrequency = 10d)
    {
        const double mmPerSecPerG = 9806.65d;
        return Math.Round(gValue * mmPerSecPerG / (2d * Math.PI * referenceHzFrequency), 4);
    }

    public static DateTime EpochMillisToUtc(long epochMs) =>
        DateTimeOffset.FromUnixTimeMilliseconds(epochMs).UtcDateTime;
}
