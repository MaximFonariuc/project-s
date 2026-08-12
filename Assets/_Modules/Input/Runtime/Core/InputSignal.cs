namespace ModularInput
{
    public enum InputSignalPhase : byte
    {
        Started = 0,
        Performed = 1,
        Canceled = 2
    }

    public enum InputDeviceKind : byte
    {
        Unknown = 0,
        KeyboardMouse = 1,
        Gamepad = 2,
        Touch = 3,
        Joystick = 4,
        XR = 5,
        Virtual = 6
    }

    public readonly struct InputSignal
    {
        public InputSourceId SourceId { get; }
        public InputMapId MapId { get; }
        public InputActionId ActionId { get; }
        public InputValue Value { get; }
        public InputSignalPhase Phase { get; }
        public InputDeviceKind DeviceKind { get; }
        public double Time { get; }
        public int Frame { get; }
        public string MapName { get; }
        public string ActionName { get; }
        public string ControlPath { get; }

        public InputSignal(
            InputSourceId sourceId,
            InputMapId mapId,
            InputActionId actionId,
            InputValue value,
            InputSignalPhase phase,
            InputDeviceKind deviceKind,
            double time,
            int frame,
            string mapName,
            string actionName,
            string controlPath)
        {
            SourceId = sourceId;
            MapId = mapId;
            ActionId = actionId;
            Value = value;
            Phase = phase;
            DeviceKind = deviceKind;
            Time = time;
            Frame = frame;
            MapName = mapName ?? string.Empty;
            ActionName = actionName ?? string.Empty;
            ControlPath = controlPath ?? string.Empty;
        }

        internal InputSignal AsCurrent(InputSignalPhase phase)
        {
            return new InputSignal(
                SourceId,
                MapId,
                ActionId,
                Value,
                phase,
                DeviceKind,
                Time,
                Frame,
                MapName,
                ActionName,
                ControlPath);
        }

        internal InputSignal AsCanceled()
        {
            return new InputSignal(
                SourceId,
                MapId,
                ActionId,
                default,
                InputSignalPhase.Canceled,
                DeviceKind,
                Time,
                Frame,
                MapName,
                ActionName,
                ControlPath);
        }
    }
}
