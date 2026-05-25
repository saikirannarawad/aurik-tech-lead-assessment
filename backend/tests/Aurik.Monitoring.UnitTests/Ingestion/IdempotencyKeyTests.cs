using Aurik.Monitoring.Application.Ingestion;
using Aurik.Monitoring.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Aurik.Monitoring.UnitTests.Ingestion;

public class IdempotencyKeyTests
{
    [Fact]
    public void Identical_payloads_for_same_vendor_produce_same_key()
    {
        var body = """{"hello":"world"}""";
        var a = IdempotencyKey.ForPayload(VendorType.PulseForge, body);
        var b = IdempotencyKey.ForPayload(VendorType.PulseForge, body);
        a.Should().Be(b);
        a.Should().StartWith("sha256:");
    }

    [Fact]
    public void Different_vendor_disambiguates_identical_body()
    {
        var body = """{"hello":"world"}""";
        var pf = IdempotencyKey.ForPayload(VendorType.PulseForge, body);
        var tw = IdempotencyKey.ForPayload(VendorType.ThermexWatch, body);
        pf.Should().NotBe(tw);
    }
}
