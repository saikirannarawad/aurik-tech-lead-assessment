using EphemeralMongo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Aurik.Monitoring.IntegrationTests;

public sealed class AurikWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly IMongoRunner _mongo;

    public AurikWebApplicationFactory()
    {
        _mongo = MongoRunner.Run(new MongoRunnerOptions { UseSingleNodeReplicaSet = false });
    }

    public string ConnectionString => _mongo.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = _mongo.ConnectionString,
                ["Mongo:DatabaseName"] = $"aurik_test_{Guid.NewGuid():N}",
                ["Seed:RunOnStartup"] = "true",
                ["Auth:VendorApiKeys:PulseForge"] = "test-pf-key",
                ["Auth:VendorApiKeys:ThermexWatch"] = "test-tw-key",
                ["Auth:VendorApiKeys:MaintaFlow"] = "test-mf-key"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _mongo.Dispose();
    }
}
