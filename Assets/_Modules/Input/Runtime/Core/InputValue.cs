using UnityEngine;

namespace ModularInput
{
    public enum InputValueKind : byte
    {
        None = 0,
        Button = 1,
        Axis1D = 2,
        Axis2D = 3,
        Axis3D = 4,
        Quaternion = 5
    }

    public readonly struct InputValue
    {
        private readonly Vector4 _data;

        public InputValueKind Kind { get; }
        public bool IsActuated => Kind != InputValueKind.None && _data.sqrMagnitude > 0.000001f;

        private InputValue(InputValueKind kind, Vector4 data)
        {
            Kind = kind;
            _data = data;
        }

        public static InputValue FromButton(bool value) =>
            new InputValue(InputValueKind.Button, new Vector4(value ? 1f : 0f, 0f, 0f, 0f));

        public static InputValue FromAxis(float value) =>
            new InputValue(InputValueKind.Axis1D, new Vector4(value, 0f, 0f, 0f));

        public static InputValue FromVector2(Vector2 value) =>
            new InputValue(InputValueKind.Axis2D, new Vector4(value.x, value.y, 0f, 0f));

        public static InputValue FromVector3(Vector3 value) =>
            new InputValue(InputValueKind.Axis3D, new Vector4(value.x, value.y, value.z, 0f));

        public static InputValue FromQuaternion(Quaternion value) =>
            new InputValue(InputValueKind.Quaternion, new Vector4(value.x, value.y, value.z, value.w));

        public bool AsButton() => _data.x >= 0.5f;
        public float AsAxis() => _data.x;
        public Vector2 AsVector2() => new Vector2(_data.x, _data.y);
        public Vector3 AsVector3() => new Vector3(_data.x, _data.y, _data.z);
        public Quaternion AsQuaternion() => new Quaternion(_data.x, _data.y, _data.z, _data.w);
    }
}
