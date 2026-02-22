#nullable enable
using System;
using UnityEditor;
using SceneBlueprint.Editor.Session;

namespace SceneBlueprint.Editor.WindowServices
{
    /// <summary>
    /// 编辑器脏调度器——统一管理分析/预览/工作台三路延迟刷新，
    /// 合并同帧内多次 MarkDirty 调用，只触发一次 delayCall。
    /// <para>
    /// 解决问题：分析控制器和预览调度器各自独立 delayCall，同帧可能双重刷新。
    /// 解决方式：所有刷新请求统一走此类，一个帧只产生一次 delayCall。
    /// </para>
    /// </summary>
    public sealed class EditorDirtyScheduler : ISessionService
    {
        [Flags]
        public enum DirtyFlag
        {
            None      = 0,
            Analysis  = 1 << 0,
            Preview   = 1 << 1,
            Workbench = 1 << 2,
            All       = Analysis | Preview | Workbench
        }

        private DirtyFlag _pendingFlags;
        private bool      _flushScheduled;
        private bool      _disposed;

        private readonly Action<DirtyFlag>? _onFlush;

        /// <param name="onFlush">flush 时被调用，参数为本次需要处理的脏标志集合</param>
        public EditorDirtyScheduler(Action<DirtyFlag> onFlush)
            => _onFlush = onFlush;

        /// <summary>标记指定维度为脏，触发延迟 Flush（同帧多次调用会合并）。</summary>
        public void MarkDirty(DirtyFlag flags)
        {
            if (_disposed) return;
            _pendingFlags |= flags;
            if (_flushScheduled) return;
            _flushScheduled = true;
            EditorApplication.delayCall += Flush;
        }

        private void Flush()
        {
            if (_disposed) { _pendingFlags = DirtyFlag.None; return; }
            _flushScheduled = false;
            var flags = _pendingFlags;
            _pendingFlags = DirtyFlag.None;
            _onFlush?.Invoke(flags);
        }

        void ISessionService.OnSessionDisposed()
        {
            _disposed = true;
            EditorApplication.delayCall -= Flush;
            _pendingFlags = DirtyFlag.None;
        }
    }
}
