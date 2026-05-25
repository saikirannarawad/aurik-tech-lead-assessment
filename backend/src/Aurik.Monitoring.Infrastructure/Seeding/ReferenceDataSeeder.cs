using System.Globalization;
using Aurik.Monitoring.Application.Abstractions.Persistence;
using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Infrastructure.Configuration;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aurik.Monitoring.Infrastructure.Seeding;

public interface IReferenceDataSeeder
{
    Task SeedAsync(CancellationToken ct);
}

public sealed class ReferenceDataSeeder : IReferenceDataSeeder
{
    private readonly IMachineRepository _machines;
    private readonly ILineRepository _lines;
    private readonly SeedOptions _options;
    private readonly ILogger<ReferenceDataSeeder> _log;

    public ReferenceDataSeeder(
        IMachineRepository machines,
        ILineRepository lines,
        IOptions<SeedOptions> options,
        ILogger<ReferenceDataSeeder> log)
    {
        _machines = machines;
        _lines = lines;
        _options = options.Value;
        _log = log;
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        var seedDir = ResolveSeedDirectory();
        if (seedDir is null)
        {
            _log.LogWarning("Seed directory '{Configured}' not found; skipping reference data seed.",
                _options.SeedDirectory);
            return;
        }

        var assetCsv = Path.Combine(seedDir, "asset_reference.csv");
        var lineCsv = Path.Combine(seedDir, "line_reference.csv");

        if (File.Exists(assetCsv))
        {
            var machines = ReadAssets(assetCsv);
            await _machines.UpsertManyAsync(machines, ct).ConfigureAwait(false);
            _log.LogInformation("Seeded {Count} machines from {File}", machines.Count, assetCsv);
        }

        if (File.Exists(lineCsv))
        {
            var lines = ReadLines(lineCsv);
            await _lines.UpsertManyAsync(lines, ct).ConfigureAwait(false);
            _log.LogInformation("Seeded {Count} lines from {File}", lines.Count, lineCsv);
        }
    }

    private string? ResolveSeedDirectory()
    {
        // Try configured path, then a few common locations so Docker + local dev both work.
        var candidates = new List<string>
        {
            _options.SeedDirectory,
            Path.Combine(AppContext.BaseDirectory, _options.SeedDirectory),
            Path.Combine(AppContext.BaseDirectory, "seed"),
            "/app/seed"
        };

        // Walk up from the binary directory looking for a sibling 'seed' folder.
        // Lets local `dotnet run` find backend/seed without env vars.
        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && probe is not null; i++, probe = probe.Parent)
        {
            candidates.Add(Path.Combine(probe.FullName, "seed"));
        }

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static List<Machine> ReadAssets(string path)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
        var rows = csv.GetRecords<AssetCsvRow>().ToList();
        return rows.Select(r => new Machine
        {
            MachineId = r.MachineId,
            PlantId = r.PlantId,
            LineId = r.LineId,
            MachineType = r.MachineType,
            Criticality = r.Criticality,
            InstalledDate = DateTime.TryParse(r.InstalledDate, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : null,
            RatedMaxTempC = r.RatedMaxTempC,
            RatedMaxVibrationMmS = r.RatedMaxVibrationMmS,
            BaselinePowerKw = r.BaselinePowerKw,
            AssetStatus = r.AssetStatus
        }).ToList();
    }

    private static List<Line> ReadLines(string path)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
        var rows = csv.GetRecords<LineCsvRow>().ToList();
        return rows.Select(r => new Line
        {
            PlantId = r.PlantId,
            LineId = r.LineId,
            LineName = r.LineName,
            OperatingWindow = r.OperatingWindow
        }).ToList();
    }
}
