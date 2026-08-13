namespace _App
{
    public interface IPlayerTraversalCommands
    {
        bool TryInteract();
        bool TryInvertGravity();
    }

    internal interface IPlayerInteractionProbe
    {
        bool HasInteractable();
    }

    internal interface IPlayerSimulationPause
    {
        bool IsPauseRequested { get; }

        void SetPaused(bool paused);
    }

    internal interface IPlayerSimulationLock
    {
        System.IDisposable AcquirePause();
    }
}
