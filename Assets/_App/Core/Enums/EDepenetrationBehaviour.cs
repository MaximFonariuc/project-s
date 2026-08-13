using System;

namespace _App
{
    [Flags]
    public enum EDepenetrationBehaviour
    {
        IgnoreNone = 0,

        IgnoreStatic = 1 << 0,
        IgnoreDynamic = 1 << 1,
        IgnoreKinematic = 1 << 2
    }
}