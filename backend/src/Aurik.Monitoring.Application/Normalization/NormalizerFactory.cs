using Aurik.Monitoring.Domain.Enums;

namespace Aurik.Monitoring.Application.Normalization;

/// <summary>
/// Factory pattern: resolves the correct INormalizer for a vendor.
/// Adding a new vendor = register one more INormalizer in DI; this factory needs no changes (OCP).
/// </summary>
public sealed class NormalizerFactory : INormalizerFactory
{
    private readonly IReadOnlyDictionary<VendorType, INormalizer> _byVendor;

    public NormalizerFactory(IEnumerable<INormalizer> normalizers)
    {
        _byVendor = normalizers.ToDictionary(n => n.Vendor);
    }

    public INormalizer GetFor(VendorType vendor)
    {
        if (!_byVendor.TryGetValue(vendor, out var normalizer))
            throw new InvalidOperationException($"No normalizer registered for vendor '{vendor}'.");
        return normalizer;
    }
}
