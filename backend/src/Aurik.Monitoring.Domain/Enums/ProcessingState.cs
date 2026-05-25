namespace Aurik.Monitoring.Domain.Enums;

public enum ProcessingState
{
    Accepted = 0,
    Queued = 1,
    Processing = 2,
    Succeeded = 3,
    PartiallySucceeded = 4,
    Failed = 5,
    DeadLettered = 6,
    Duplicate = 7
}
