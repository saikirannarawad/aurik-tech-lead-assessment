using Aurik.Monitoring.Application.DerivedView;
using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Aurik.Monitoring.UnitTests.DerivedView;

public class DerivedStatusCalculatorTests
{
    private readonly DerivedStatusCalculator _sut = new();
    private static readonly DateTime Now = new(2026, 4, 18, 9, 0, 0, DateTimeKind.Utc);

    private static Machine Press => new()
    {
        MachineId = "EQ-001", PlantId = "PLANT_01", LineId = "LINE-A",
        MachineType = "Press", Criticality = "high",
        RatedMaxTempC = 85.0, RatedMaxVibrationMmS = 9.0, BaselinePowerKw = 36.0,
        AssetStatus = "active"
    };

    private static NormalizedEvent Event(
        CanonicalEventType type,
        DateTime time,
        AttentionLevel severity = AttentionLevel.None,
        double? vibration = null,
        double? tempC = null,
        double? powerKw = null,
        double? sensorHealth = null,
        string? maintenanceStatus = null,
        string? inspectionResult = null,
        int? daysSinceLastService = null) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        RawPayloadId = "raw",
        Vendor = VendorType.PulseForge,
        VendorEventId = Guid.NewGuid().ToString(),
        IdempotencyKey = Guid.NewGuid().ToString(),
        MachineId = "EQ-001", PlantId = "PLANT_01", LineId = "LINE-A",
        EventTimeUtc = time, ProcessedAtUtc = time, EventType = type, SeverityHint = severity,
        VibrationMmPerSec = vibration, TemperatureCelsius = tempC, PowerKw = powerKw,
        SensorHealth = sensorHealth, MaintenanceStatus = maintenanceStatus,
        InspectionResult = inspectionResult, DaysSinceLastService = daysSinceLastService
    };

    [Fact]
    public void Returns_unknown_when_no_events()
    {
        var view = _sut.Compute(Press, Array.Empty<NormalizedEvent>(), Now);
        view.DerivedStatus.Should().Be(DerivedStatus.Unknown);
        view.NeedsAttention.Should().BeFalse();
    }

    [Fact]
    public void Marks_stale_when_latest_event_too_old()
    {
        var stale = Event(CanonicalEventType.HighVibration, Now.AddDays(-3), AttentionLevel.High, vibration: 12);
        var view = _sut.Compute(Press, new[] { stale }, Now);
        view.DerivedStatus.Should().Be(DerivedStatus.Stale);
        view.NeedsAttention.Should().BeTrue();
        view.ReasonCodes.Should().Contain(ReasonCode.StaleSignal);
    }

    [Fact]
    public void Vibration_above_threshold_triggers_high_attention_and_reason_code()
    {
        var ev = Event(CanonicalEventType.HighVibration, Now.AddMinutes(-10),
            severity: AttentionLevel.High, vibration: 11.8);
        var view = _sut.Compute(Press, new[] { ev }, Now);
        view.ReasonCodes.Should().Contain(ReasonCode.VibrationOverThreshold);
        view.AttentionLevel.Should().Be(AttentionLevel.High);
        view.DerivedStatus.Should().Be(DerivedStatus.AtRisk);
        view.NeedsAttention.Should().BeTrue();
        view.SourceEventRefs.Should().Contain(ev.Id);
    }

    [Fact]
    public void Critical_temperature_overrides_lower_signals()
    {
        var mild = Event(CanonicalEventType.OperatorNote, Now.AddMinutes(-20), AttentionLevel.Low);
        var crit = Event(CanonicalEventType.HighTemperature, Now.AddMinutes(-5),
            severity: AttentionLevel.Critical, tempC: 96.4);
        var view = _sut.Compute(Press, new[] { mild, crit }, Now);
        view.AttentionLevel.Should().Be(AttentionLevel.Critical);
        view.DerivedStatus.Should().Be(DerivedStatus.Critical);
        view.ReasonCodes.Should().Contain(ReasonCode.TemperatureOverThreshold);
        view.ReasonCodes.Should().Contain(ReasonCode.VendorReportedCritical);
    }

    [Fact]
    public void Recovery_after_concerning_events_does_not_drop_critical_state()
    {
        var crit = Event(CanonicalEventType.HighVibration, Now.AddMinutes(-20),
            severity: AttentionLevel.Critical, vibration: 20);
        var recovery = Event(CanonicalEventType.Recovery, Now.AddMinutes(-2), AttentionLevel.Low);
        var view = _sut.Compute(Press, new[] { crit, recovery }, Now);
        view.AttentionLevel.Should().Be(AttentionLevel.Critical, because: "recovery should not silently downgrade critical events");
    }

    [Fact]
    public void Maintenance_overdue_raises_attention()
    {
        var ev = Event(CanonicalEventType.MaintenanceUpdate, Now.AddMinutes(-30),
            maintenanceStatus: "overdue", daysSinceLastService: 90);
        var view = _sut.Compute(Press, new[] { ev }, Now);
        view.ReasonCodes.Should().Contain(ReasonCode.MaintenanceOverdue);
        view.AttentionLevel.Should().Be(AttentionLevel.High);
    }

    [Fact]
    public void Under_maintenance_asset_status_is_respected()
    {
        var pressInMaintenance = new Machine
        {
            MachineId = "EQ-007", PlantId = "PLANT_02", LineId = "LINE-E",
            AssetStatus = "maintenance",
            RatedMaxTempC = 84, RatedMaxVibrationMmS = 7.8, BaselinePowerKw = 16
        };
        var ev = new NormalizedEvent
        {
            Id = Guid.NewGuid().ToString("N"), RawPayloadId = "r", Vendor = VendorType.PulseForge,
            VendorEventId = "x", IdempotencyKey = "x",
            MachineId = "EQ-007", PlantId = "PLANT_02", LineId = "LINE-E",
            EventTimeUtc = Now.AddMinutes(-5), ProcessedAtUtc = Now,
            EventType = CanonicalEventType.NominalSignal, SeverityHint = AttentionLevel.None
        };

        var view = _sut.Compute(pressInMaintenance, new[] { ev }, Now);
        view.DerivedStatus.Should().Be(DerivedStatus.UnderMaintenance);
    }
}
