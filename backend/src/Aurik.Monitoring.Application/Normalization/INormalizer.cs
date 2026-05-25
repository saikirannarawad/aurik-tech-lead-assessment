using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Domain.Enums;

namespace Aurik.Monitoring.Application.Normalization;

/// <summary>
/// Each vendor has its own normalizer that turns a raw payload string into a list of canonical events.
/// Factory chooses the implementation by VendorType. New vendors plug in by adding a new INormalizer
/// implementation and registering it in DI — no other code changes.
/// </summary>
public interface INormalizer
{
    VendorType Vendor { get; }

    NormalizationResult Normalize(string rawJson, string rawPayloadId);
}

public sealed record NormalizationResult(
    IReadOnlyList<NormalizedEvent> Events,
    IReadOnlyList<NormalizationIssue> Issues);

public sealed record NormalizationIssue(string Locator, string Reason);
