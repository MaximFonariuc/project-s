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

    public interface IPlayerLadderGrip
    {
        bool IsGripActive { get; }
        Ladder ActiveLadder { get; }
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
