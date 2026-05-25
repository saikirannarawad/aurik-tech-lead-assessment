using Aurik.Monitoring.Domain.Enums;

namespace Aurik.Monitoring.Application.Normalization;

public interface INormalizerFactory
{
    INormalizer GetFor(VendorType vendor);
}
