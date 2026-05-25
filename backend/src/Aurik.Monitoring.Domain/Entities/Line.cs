namespace Aurik.Monitoring.Domain.Entities;

public sealed class Line
{
    public required string PlantId { get; init; }
    public required string LineId { get; init; }
    public string? LineName { get; init; }
    public string? OperatingWindow { get; init; }
}
