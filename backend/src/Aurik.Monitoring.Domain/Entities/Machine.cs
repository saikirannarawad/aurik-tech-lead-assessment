namespace Aurik.Monitoring.Domain.Entities;

public sealed class Machine
{
    public required string MachineId { get; init; }
    public required string PlantId { get; init; }
    public required string LineId { get; init; }
    public string? MachineType { get; init; }
    public string? Criticality { get; init; }
    public DateTime? InstalledDate { get; init; }
    public double? RatedMaxTempC { get; init; }
    public double? RatedMaxVibrationMmS { get; init; }
    public double? BaselinePowerKw { get; init; }
    public string? AssetStatus { get; init; }
}
