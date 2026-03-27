#nullable enable
using System;

namespace SceneBlueprint.Runtime.State
{
    [Flags]
    public enum StateLifecycleDisposition
    {
        None = 0,
        Created = 1 << 0,
        Reused = 1 << 1,
        KeepReadOnlyFinal = 1 << 2,
        DeferredRelease = 1 << 3,
        Released = 1 << 4,
    }
}
