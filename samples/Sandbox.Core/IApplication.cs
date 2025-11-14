namespace Sandbox;

public interface IApplication
{
    event EventHandler<StatusUpdatedEventArgs>? StatusUpdated;
    ApplicationStatus GetStatus();
    void Run();
}
