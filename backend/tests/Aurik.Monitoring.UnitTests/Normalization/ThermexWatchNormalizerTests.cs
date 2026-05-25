using Aurik.Monitoring.Application.Normalization;
using Aurik.Monitoring.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Aurik.Monitoring.UnitTests.Normalization;

public class ThermexWatchNormalizerTests
{
    private readonly ThermexWatchNormalizer _sut = new();

    [Fact]
    public void Converts_fahrenheit_to_celsius_and_g_to_mm_per_sec()
    {
        const string json = """
        {
          "source": "ThermexWatch",
          "site_code": "PLANT_01",
          "readings": [{
            "readingId": "TW-8801", "assetCode": "EQ-001", "productionLine": "A",
            "timestampMs": 1776499152000, "alertCode": "VIB_WARN", "level": 4,
            "vibration_g": 0.81, "temperature_f": 181.2, "power_kw": 37.8,
            "is_active": true, "signal_quality": "GOOD"
          }]
        }
        """;

        var result = _sut.Normalize(json, "raw-1");

        result.Issues.Should().BeEmpty();
        var ev = result.Events.Should().ContainSingle().Subject;
        ev.LineId.Should().Be("LINE-A", because: "short line codes must be canonicalized");
        ev.TemperatureCelsius.Should().BeApproximately(82.8889, 0.01);
        ev.VibrationMmPerSec.Should().BeGreaterThan(0);
        ev.PowerKw.Should().Be(37.8);
        ev.EventType.Should().Be(CanonicalEventType.HighVibration);
        ev.SeverityHint.Should().Be(AttentionLevel.High);
    }

    [Fact]
    public void Skips_reading_with_missing_timestamp()
    {
        const string json = """
        {
          "source": "ThermexWatch",
          "site_code": "PLANT_01",
          "readings": [
            { "readingId": "TW-1", "assetCode": "EQ-001", "timestampMs": null, "alertCode": "OK" }
          ]
        }
        """;

        var result = _sut.Normalize(json, "raw-1");
        result.Events.Should().BeEmpty();
        result.Issues.Should().ContainSingle().Which.Reason.Should().Contain("timestampMs");
    }

    [Fact]
    public void Maps_level_to_attention_level()
    {
        const string json = """
        {
          "source": "ThermexWatch", "site_code": "PLANT_01",
          "readings": [{
            "readingId": "TW-CRIT", "assetCode": "EQ-001", "timestampMs": 1776499000000,
            "alertCode": "TEMP_CRIT", "level": 5, "is_active": true
          }]
        }
        """;
        var ev = _sut.Normalize(json, "raw-1").Events.Single();
        ev.SeverityHint.Should().Be(AttentionLevel.Critical);
        ev.EventType.Should().Be(CanonicalEventType.HighTemperature);
    }
}
