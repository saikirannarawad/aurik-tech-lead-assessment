using Aurik.Monitoring.Domain.Enums;

namespace Aurik.Monitoring.Domain.Entities;

/// <summary>
/// Derived machine-level attention view served to downstream consumers.
/// Recomputed on every successful normalization for the machine.
/// </summary>
public sealed class MachineOperationalView
{
    public required string MachineId { get; set; }
    public required string PlantId { get; set; }
    public required string LineId { get; set; }

    public DerivedStatus DerivedStatus { get; set; } = DerivedStatus.Unknown;
    public bool NeedsAttention { get; set; }
    public AttentionLevel AttentionLevel { get; set; } = AttentionLevel.None;

    public List<string> ReasonCodes { get; set; } = new();
    public DateTime? LatestRelevantEventTime { get; set; }
    public DateTime? LastProcessedAt { get; set; }
    public ProcessingState ProcessingStatus { get; set; } = ProcessingState.Accepted;

    /// <summary>Pointers back to the source normalized-event IDs that drove the current view.</summary>
    public List<string> SourceEventRefs { get; set; } = new();

    /// <summary>Optimistic concurrency token. Incremented on every write.</summary>
    public long Version { get; set; }
}
