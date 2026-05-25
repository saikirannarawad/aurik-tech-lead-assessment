using Aurik.Monitoring.Application.Normalization;
using Aurik.Monitoring.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Aurik.Monitoring.UnitTests.Normalization;

public class NormalizerFactoryTests
{
    [Fact]
    public void Returns_correct_normalizer_per_vendor()
    {
        var factory = new NormalizerFactory(new INormalizer[]
        {
            new PulseForgeNormalizer(),
            new ThermexWatchNormalizer(),
            new MaintaFlowNormalizer()
        });

        factory.GetFor(VendorType.PulseForge).Should().BeOfType<PulseForgeNormalizer>();
        factory.GetFor(VendorType.ThermexWatch).Should().BeOfType<ThermexWatchNormalizer>();
        factory.GetFor(VendorType.MaintaFlow).Should().BeOfType<MaintaFlowNormalizer>();
    }

    [Fact]
    public void Throws_when_vendor_unknown()
    {
        var factory = new NormalizerFactory(new INormalizer[] { new PulseForgeNormalizer() });
        var act = () => factory.GetFor(VendorType.ThermexWatch);
        act.Should().Throw<InvalidOperationException>().WithMessage("*No normalizer registered*");
    }
}
