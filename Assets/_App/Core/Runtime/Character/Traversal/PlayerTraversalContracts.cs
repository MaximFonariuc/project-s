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
        bool IsBaseTransition { get; }
        float ClimbCycles { get; }
        Ladder ActiveLadder { get; }
    }

    internal interface IPlayerLadderClimbAnimation
    {
        void SetClimbPhases(float ascendPhase, float descendPhase);
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
