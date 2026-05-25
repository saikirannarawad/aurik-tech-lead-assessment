using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Domain.Enums;

namespace Aurik.Monitoring.Application.DerivedView;

/// <summary>
/// Deterministic rule-based attention calculator.
/// Inputs: machine asset reference + recent normalized events. Output: an explainable view.
/// Rules favor the most severe contributor across the window — see README for full table.
/// </summary>
public sealed class DerivedStatusCalculator : IDerivedStatusCalculator
{
    /// <summary>Events older than this fall outside the attention window.</summary>
    public static readonly TimeSpan AttentionWindow = TimeSpan.FromHours(24);

    /// <summary>If no event for this long, the view is marked Stale.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(48);

    public MachineOperationalView Compute(
        Machine machine,
        IReadOnlyList<NormalizedEvent> recentEvents,
        DateTime nowUtc)
    {
        var view = new MachineOperationalView
        {
            MachineId = machine.MachineId,
            PlantId = machine.PlantId,
            LineId = machine.LineId,
            LastProcessedAt = nowUtc,
            ProcessingStatus = ProcessingState.Succeeded
        };

        if (recentEvents.Count == 0)
        {
            view.DerivedStatus = DerivedStatus.Unknown;
            view.AttentionLevel = AttentionLevel.None;
            view.NeedsAttention = false;
            return view;
        }

        var ordered = recentEvents.OrderByDescending(e => e.EventTimeUtc).ToList();
        var latest = ordered[0];
        view.LatestRelevantEventTime = latest.EventTimeUtc;

        // Stale check first — overrides everything else.
        if (nowUtc - latest.EventTimeUtc > StaleAfter)
        {
            view.DerivedStatus = DerivedStatus.Stale;
            view.AttentionLevel = AttentionLevel.Low;
            view.NeedsAttention = true;
            view.ReasonCodes.Add(ReasonCode.StaleSignal);
            view.SourceEventRefs.Add(latest.Id);
            return view;
        }

        if (machine.AssetStatus?.Equals("maintenance", StringComparison.OrdinalIgnoreCase) == true)
        {
            view.DerivedStatus = DerivedStatus.UnderMaintenance;
        }

        var reasons = new HashSet<string>(StringComparer.Ordinal);
        var refs = new HashSet<string>(StringComparer.Ordinal);
        var maxLevel = AttentionLevel.None;
        var windowStart = nowUtc - AttentionWindow;

        foreach (var ev in ordered.Where(e => e.EventTimeUtc >= windowStart))
        {
            var (eventReasons, eventLevel) = EvaluateEvent(ev, machine);
            if (eventReasons.Count == 0 && eventLevel == AttentionLevel.None) continue;

            foreach (var r in eventReasons) reasons.Add(r);
            refs.Add(ev.Id);
            if (eventLevel > maxLevel) maxLevel = eventLevel;
        }

        // Recovery signal lowers attention if observed after concerning events.
        var hasRecovery = ordered.Any(e => e.EventType == CanonicalEventType.Recovery
                                           && e.EventTimeUtc >= windowStart);
        if (hasRecovery && maxLevel <= AttentionLevel.Moderate)
        {
            reasons.Add(ReasonCode.RecoveryObserved);
            // Keep attention low if everything we saw is mild and a recovery happened most recently.
            if (latest.EventType == CanonicalEventType.Recovery && maxLevel < AttentionLevel.High)
            {
                maxLevel = AttentionLevel.None;
            }
        }

        view.AttentionLevel = maxLevel;
        view.NeedsAttention = maxLevel >= AttentionLevel.Moderate;
        view.ReasonCodes = reasons.ToList();
        view.SourceEventRefs = refs.ToList();
        view.DerivedStatus = view.DerivedStatus == DerivedStatus.UnderMaintenance
            ? DerivedStatus.UnderMaintenance
            : MapLevelToStatus(maxLevel);

        return view;
    }

    private static (List<string> reasons, AttentionLevel level) EvaluateEvent(NormalizedEvent ev, Machine machine)
    {
        var reasons = new List<string>();
        var level = ev.SeverityHint;

        if (ev.VibrationMmPerSec is { } v && machine.RatedMaxVibrationMmS is { } maxV && v > maxV)
        {
            reasons.Add(ReasonCode.VibrationOverThreshold);
            level = Max(level, AttentionLevel.High);
        }

        if (ev.TemperatureCelsius is { } t && machine.RatedMaxTempC is { } maxT && t > maxT)
        {
            reasons.Add(ReasonCode.TemperatureOverThreshold);
            level = Max(level, AttentionLevel.High);
        }

        if (ev.PowerKw is { } p && machine.BaselinePowerKw is { } baseP && baseP > 0)
        {
            var deviation = Math.Abs(p - baseP) / baseP;
            if (deviation >= 0.30)
            {
                reasons.Add(ReasonCode.PowerAnomaly);
                level = Max(level, AttentionLevel.Moderate);
            }
        }

        if (ev.SensorHealth is { } sh && sh < 0.80)
        {
            reasons.Add(ReasonCode.SensorHealthLow);
            level = Max(level, AttentionLevel.Low);
        }

        switch (ev.EventType)
        {
            case CanonicalEventType.MaintenanceUpdate:
            case CanonicalEventType.Inspection:
                if (string.Equals(ev.MaintenanceStatus, "overdue", StringComparison.OrdinalIgnoreCase)
                    || ev.DaysSinceLastService is >= 60)
                {
                    reasons.Add(ReasonCode.MaintenanceOverdue);
                    level = Max(level, AttentionLevel.High);
                }
                else if (string.Equals(ev.MaintenanceStatus, "due_soon", StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add(ReasonCode.MaintenanceDueSoon);
                    level = Max(level, AttentionLevel.Low);
                }
                if (string.Equals(ev.InspectionResult, "minor_defect_found", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ev.InspectionResult, "major_defect_found", StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add(ReasonCode.InspectionDefect);
                    level = Max(level, AttentionLevel.Moderate);
                }
                break;
            case CanonicalEventType.OperatorNote:
                if (!string.IsNullOrWhiteSpace(ev.Note))
                {
                    reasons.Add(ReasonCode.OperatorConcern);
                    level = Max(level, AttentionLevel.Low);
                }
                break;
        }

        // Use vendor's own severity as a fallback signal so we don't silently drop critical alerts
        // even when our own thresholds aren't tripped.
        switch (ev.SeverityHint)
        {
            case AttentionLevel.Critical:
                reasons.Add(ReasonCode.VendorReportedCritical);
                break;
            case AttentionLevel.High:
                reasons.Add(ReasonCode.VendorReportedHigh);
                break;
        }

        return (reasons, level);
    }

    private static AttentionLevel Max(AttentionLevel a, AttentionLevel b) => a > b ? a : b;

    private static DerivedStatus MapLevelToStatus(AttentionLevel level) => level switch
    {
        AttentionLevel.Critical => DerivedStatus.Critical,
        AttentionLevel.High => DerivedStatus.AtRisk,
        AttentionLevel.Moderate => DerivedStatus.Degraded,
        AttentionLevel.Low => DerivedStatus.Healthy,
        _ => DerivedStatus.Healthy
    };
}
