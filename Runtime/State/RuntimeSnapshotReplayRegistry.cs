#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public interface IRuntimeStateSnapshotReplayer
    {
        bool CanReplay(RuntimeStateSnapshotPayload payload, SnapshotRestoreMode restoreMode);

        RuntimeSnapshotReplayEntryResult Replay(
            RuntimeSnapshotReplayContext context,
            RuntimeStateSnapshotPayload payload,
            SnapshotRestoreMode restoreMode);
    }

    public sealed class RuntimeSnapshotReplayRegistry
    {
        private readonly List<IRuntimeStateSnapshotReplayer> _replayers = new();

        public void Register(IRuntimeStateSnapshotReplayer replayer)
        {
            if (replayer is null)
            {
                throw new ArgumentNullException(nameof(replayer));
            }

            _replayers.Add(replayer);
        }

        public bool HasReplayer<TReplayer>()
            where TReplayer : class, IRuntimeStateSnapshotReplayer
        {
            for (var index = 0; index < _replayers.Count; index++)
            {
                if (_replayers[index] is TReplayer)
                {
                    return true;
                }
            }

            return false;
        }

        public bool RegisterUnique<TReplayer>(TReplayer replayer)
            where TReplayer : class, IRuntimeStateSnapshotReplayer
        {
            if (replayer is null)
            {
                throw new ArgumentNullException(nameof(replayer));
            }

            if (HasReplayer<TReplayer>())
            {
                return false;
            }

            _replayers.Add(replayer);
            return true;
        }

        public bool TryReplay(
            RuntimeSnapshotReplayContext context,
            RuntimeStateSnapshotPayload payload,
            SnapshotRestoreMode restoreMode,
            out RuntimeSnapshotReplayEntryResult replayResult)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (payload is null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (TryResolveReplayer(payload, restoreMode, out var replayer))
            {
                replayResult = replayer.Replay(context, payload, restoreMode);
                return true;
            }

            replayResult = RuntimeSnapshotReplayEntryResult.Failed(
                context.SnapshotEntry.LogicalEntryKey,
                $"No snapshot replayer was registered for schema '{payload.SchemaId}' with mode '{restoreMode}'.",
                payload.SchemaVersion,
                payload.SchemaVersion);
            return false;
        }

        public bool TryResolveReplayer(
            RuntimeStateSnapshotPayload payload,
            SnapshotRestoreMode restoreMode,
            out IRuntimeStateSnapshotReplayer replayer)
        {
            if (payload is null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            for (var index = 0; index < _replayers.Count; index++)
            {
                var candidate = _replayers[index];
                if (!candidate.CanReplay(payload, restoreMode))
                {
                    continue;
                }

                replayer = candidate;
                return true;
            }

            replayer = null!;
            return false;
        }
    }
}
