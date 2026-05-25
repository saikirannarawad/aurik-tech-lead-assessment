using Aurik.Monitoring.Application.Normalization;
using Aurik.Monitoring.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Aurik.Monitoring.UnitTests.Normalization;

public class MaintaFlowNormalizerTests
{
    private readonly MaintaFlowNormalizer _sut = new();

    [Fact]
    public void Parses_non_iso_timestamp_format()
    {
        const string json = """
        {
          "provider_name": "MaintaFlow",
          "factory_id": "PLANT_01",
          "records": [{
            "record_id": "MF-501", "machine_ref": "EQ-001", "line_ref": "LINE-A",
            "recorded_at": "2026/04/18 08:14:05", "record_type": "inspection",
            "inspection_result": "minor_defect_found", "maintenance_status": "not_due",
            "days_since_last_service": 18, "manual_confidence": "medium"
          }]
        }
        """;

        var result = _sut.Normalize(json, "raw-1");
        var ev = result.Events.Should().ContainSingle().Subject;
        ev.EventTimeUtc.Should().Be(new DateTime(2026, 4, 18, 8, 14, 5, DateTimeKind.Utc));
        ev.EventType.Should().Be(CanonicalEventType.Inspection);
        ev.InspectionResult.Should().Be("minor_defect_found");
        ev.SeverityHint.Should().Be(AttentionLevel.Moderate, because: "minor defect raises severity hint to moderate");
    }

    [Fact]
    public void Canonicalizes_short_line_ref()
    {
        const string json = """
        {
          "provider_name": "MaintaFlow", "factory_id": "PLANT_01",
          "records": [{
            "record_id": "MF-X", "machine_ref": "EQ-004", "line_ref": "C",
            "recorded_at": "2026/04/18 07:40:00", "record_type": "operator_note",
            "technician_note": "intermittent noise"
          }]
        }
        """;
        var ev = _sut.Normalize(json, "raw-1").Events.Single();
        ev.LineId.Should().Be("LINE-C");
        ev.EventType.Should().Be(CanonicalEventType.OperatorNote);
    }
}
