using Aurik.Monitoring.Application.Normalization;
using Aurik.Monitoring.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Aurik.Monitoring.UnitTests.Normalization;

public class PulseForgeNormalizerTests
{
    private readonly PulseForgeNormalizer _sut = new();

    [Fact]
    public void Maps_happy_path_payload_to_canonical_events()
    {
        const string json = """
        {
          "vendor": "PulseForge",
          "plant_id": "PLANT_01",
          "batch_generated_at": "2026-04-18T08:05:00Z",
          "events": [
            {
              "event_id": "PF-1001",
              "machine_id": "EQ-001",
              "line_id": "LINE-A",
              "event_time": "2026-04-18T07:59:12Z",
              "event_type": "HIGH_VIBRATION",
              "severity": "high",
              "vibration_mm_s": 11.8,
              "temperature_c": 83.2,
              "machine_state": "running",
              "sensor_health": 0.91,
              "vendor_confidence": 0.87
            }
          ]
        }
        """;

        var result = _sut.Normalize(json, rawPayloadId: "raw-1");

        result.Issues.Should().BeEmpty();
        result.Events.Should().HaveCount(1);
        var ev = result.Events[0];
        ev.Vendor.Should().Be(VendorType.PulseForge);
        ev.VendorEventId.Should().Be("PF-1001");
        ev.IdempotencyKey.Should().Be("PulseForge:PF-1001");
        ev.MachineId.Should().Be("EQ-001");
        ev.PlantId.Should().Be("PLANT_01");
        ev.LineId.Should().Be("LINE-A");
        ev.EventType.Should().Be(CanonicalEventType.HighVibration);
        ev.SeverityHint.Should().Be(AttentionLevel.High);
        ev.VibrationMmPerSec.Should().Be(11.8);
        ev.TemperatureCelsius.Should().Be(83.2);
        ev.EventTimeUtc.Should().Be(new DateTime(2026, 4, 18, 7, 59, 12, DateTimeKind.Utc));
    }

    [Fact]
    public void Records_issue_for_missing_event_id_but_continues()
    {
        const string json = """
        {
          "vendor": "PulseForge",
          "plant_id": "PLANT_01",
          "events": [
            { "event_id": null, "machine_id": "EQ-001", "event_time": "2026-04-18T07:00:00Z", "event_type": "HIGH_VIBRATION" },
            { "event_id": "PF-X", "machine_id": "EQ-002", "event_time": "2026-04-18T07:01:00Z", "event_type": "TEMP_SPIKE" }
          ]
        }
        """;

        var result = _sut.Normalize(json, "raw-1");

        result.Issues.Should().HaveCount(1);
        result.Issues[0].Reason.Should().Contain("event_id");
        result.Events.Should().HaveCount(1);
        result.Events[0].VendorEventId.Should().Be("PF-X");
    }

    [Fact]
    public void Maps_recovery_signal_to_canonical_recovery_type()
    {
        const string json = """
        {
          "vendor": "PulseForge",
          "plant_id": "PLANT_01",
          "events": [{
            "event_id": "PF-R", "machine_id": "EQ-001", "line_id": "LINE-A",
            "event_time": "2026-04-18T08:00:00Z", "event_type": "RECOVERY_SIGNAL", "severity": "low"
          }]
        }
        """;

        var result = _sut.Normalize(json, "raw-1");
        result.Events.Single().EventType.Should().Be(CanonicalEventType.Recovery);
    }
}
