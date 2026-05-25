using CsvHelper.Configuration.Attributes;

namespace Aurik.Monitoring.Infrastructure.Seeding;

internal sealed class AssetCsvRow
{
    [Name("machine_id")] public string MachineId { get; set; } = "";
    [Name("plant_id")] public string PlantId { get; set; } = "";
    [Name("line_id")] public string LineId { get; set; } = "";
    [Name("machine_type")] public string? MachineType { get; set; }
    [Name("criticality")] public string? Criticality { get; set; }
    [Name("installed_date")] public string? InstalledDate { get; set; }
    [Name("rated_max_temp_c")] public double? RatedMaxTempC { get; set; }
    [Name("rated_max_vibration_mm_s")] public double? RatedMaxVibrationMmS { get; set; }
    [Name("baseline_power_kw")] public double? BaselinePowerKw { get; set; }
    [Name("asset_status")] public string? AssetStatus { get; set; }
}

internal sealed class LineCsvRow
{
    [Name("plant_id")] public string PlantId { get; set; } = "";
    [Name("line_id")] public string LineId { get; set; } = "";
    [Name("line_name")] public string? LineName { get; set; }
    [Name("operating_window")] public string? OperatingWindow { get; set; }
}
