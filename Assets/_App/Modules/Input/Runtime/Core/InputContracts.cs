using System;

namespace ModularInput
{
    public interface IInputReader
    {
        event Action<InputSignal> SignalChanged;
        bool TryReadValue(InputActionId actionId, out InputValue value);
    }

    public interface IInputMapController
    {
        bool SetMapEnabled(InputMapId mapId, bool enabled);
        void DisableAllMaps();
    }

    public interface IInputBindingOverrides
    {
        bool ApplyBindingOverride(InputActionId actionId, int bindingIndex, string controlPath);
        bool RemoveBindingOverride(InputActionId actionId, int bindingIndex);
        string SaveBindingOverrides();
        bool LoadBindingOverrides(string json, bool removeExisting = true);
    }

    public interface IInputSource : IInputMapController, IDisposable
    {
        event Action<InputSignal> SignalEmitted;
        InputSourceId SourceId { get; }
        bool IsRunning { get; }
        void Start();
        void Stop();
    }

    public interface IInputService : IInputReader, IInputMapController, IInputBindingOverrides, IDisposable
    {
        bool IsRunning { get; }
        void Start();
        void Stop();
        bool AttachSource(IInputSource source);
        bool DetachSource(InputSourceId sourceId);
    }
}
