namespace FireflyFramework.Orchestration.Core;

public enum ExecutionStatus
{
    Pending, Running, Waiting, Suspended, Completed, Failed, Cancelled, TimedOut,
    Trying, Confirming, Confirmed, Cancelling, Canceled, Compensating,
}

public enum StepStatus { NotStarted, Running, Completed, Failed, Skipped, Retrying, Compensated, CompensationFailed }
public enum ExecutionPattern { Workflow, Saga, Tcc }
public enum TriggerMode { Sync, Async }
public enum TccPhase { Try, Confirm, Cancel }
