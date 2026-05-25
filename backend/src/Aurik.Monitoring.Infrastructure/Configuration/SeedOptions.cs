namespace Aurik.Monitoring.Infrastructure.Configuration;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Run the reference-data seeder on app startup.</summary>
    public bool RunOnStartup { get; set; } = true;

    /// <summary>Absolute or relative path to the directory containing asset_reference.csv and line_reference.csv.</summary>
    public string SeedDirectory { get; set; } = "seed";
}
