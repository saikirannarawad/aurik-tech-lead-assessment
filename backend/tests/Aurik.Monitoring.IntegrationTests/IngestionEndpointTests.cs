using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Aurik.Monitoring.IntegrationTests;

public class IngestionEndpointTests : IClassFixture<AurikWebApplicationFactory>
{
    private readonly AurikWebApplicationFactory _factory;
    public IngestionEndpointTests(AurikWebApplicationFactory factory) => _factory = factory;

    private const string PulseForgeBody = """
    {
      "vendor": "PulseForge",
      "plant_id": "PLANT_01",
      "events": [{
        "event_id": "PF-INT-1", "machine_id": "EQ-001", "line_id": "LINE-A",
        "event_time": "2026-04-18T07:59:12Z", "event_type": "HIGH_VIBRATION",
        "severity": "high", "vibration_mm_s": 11.8, "temperature_c": 83.2
      }]
    }
    """;

    [Fact]
    public async Task Rejects_request_without_api_key()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/ingestion/pulseforge",
            new StringContent(PulseForgeBody, Encoding.UTF8, "application/json"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Accepts_payload_with_valid_api_key()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Vendor-Api-Key", "test-pf-key");

        var resp = await client.PostAsync("/api/ingestion/pulseforge",
            new StringContent(PulseForgeBody, Encoding.UTF8, "application/json"));
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var body = await resp.Content.ReadFromJsonAsync<IngestAck>();
        body.Should().NotBeNull();
        body!.duplicate.Should().BeFalse();
        body.state.Should().Be("Queued");
    }

    [Fact]
    public async Task Replays_of_same_payload_collapse_via_idempotency()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Vendor-Api-Key", "test-pf-key");

        var body = new StringContent(PulseForgeBody, Encoding.UTF8, "application/json");
        await client.PostAsync("/api/ingestion/pulseforge", body);

        var resp = await client.PostAsync("/api/ingestion/pulseforge",
            new StringContent(PulseForgeBody, Encoding.UTF8, "application/json"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: "second submission of same body is a duplicate");

        var ack = await resp.Content.ReadFromJsonAsync<IngestAck>();
        ack!.duplicate.Should().BeTrue();
        ack.state.Should().Be("Duplicate");
    }

    private sealed record IngestAck(string raw_payload_id, string state, bool duplicate, int record_count, string idempotency_key);
}
