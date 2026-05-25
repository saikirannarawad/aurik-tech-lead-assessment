using Aurik.Monitoring.Domain.Entities;

namespace Aurik.Monitoring.Application.DerivedView;

public interface IDerivedStatusCalculator
{
    /// <summary>
    /// Compute the derived attention view for a machine given its asset reference and most recent
    /// normalized events. Logic is deterministic and explainable — every output flag corresponds to
    /// at least one reason code derived from a concrete source event.
    /// </summary>
    MachineOperationalView Compute(
        Machine machine,
        IReadOnlyList<NormalizedEvent> recentEvents,
        DateTime nowUtc);
}
