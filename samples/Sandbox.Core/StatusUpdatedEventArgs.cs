namespace Sandbox;

public class StatusUpdatedEventArgs : EventArgs
{
    public ApplicationStatus Status { get; }

    public StatusUpdatedEventArgs(ApplicationStatus status)
    {
        Status = status;
    }
}
