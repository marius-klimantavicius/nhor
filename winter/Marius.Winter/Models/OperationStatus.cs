namespace Marius.Winter;

public enum OperationState { Empty, Working, Completed, Failed, Canceled }
public enum StatusSeverity { Information, Success, Warning, Error }

/// <summary>Application-owned operation presentation; null progress means busy animation while working.</summary>
public sealed record OperationStatus(
    string Message = "Ready to start.",
    OperationState State = OperationState.Empty,
    StatusSeverity Severity = StatusSeverity.Information,
    float? Progress = null)
{
    public bool IsBusy => State == OperationState.Working;
    public bool CanRetry => State is OperationState.Failed or OperationState.Canceled;
}
