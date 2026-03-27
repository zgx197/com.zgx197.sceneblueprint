#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public interface IRuntimeStateSnapshotReplayPlanner
    {
        int GetReplayPriority(
            RuntimeSnapshotReplayContext context,
            RuntimeStateSnapshotPayload payload,
            SnapshotRestoreMode restoreMode);

        IReadOnlyList<string>? GetReplayDependencies(
            RuntimeSnapshotReplayContext context,
            RuntimeStateSnapshotPayload payload,
            SnapshotRestoreMode restoreMode);
    }
}
