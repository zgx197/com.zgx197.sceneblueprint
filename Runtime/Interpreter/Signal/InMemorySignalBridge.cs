#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 内存信号桥——Package 侧默认 <see cref="ISignalBridge"/> 实现。
    /// <para>
    /// 包装 <see cref="ISignalBus"/>，在帧末分发已发射信号给监听器，
    /// 在 Send 时将信号注入 Bus 供蓝图消费。
    /// </para>
    /// </summary>
    public class InMemorySignalBridge : IBlueprintEventContextSignalBridge
    {
        private readonly ISignalBus _bus;
        private readonly List<ListenerEntry> _listeners = new();

        // 防止在遍历 _listeners 时修改
        private bool _dispatching;
        private readonly List<ListenerEntry> _pendingAdds = new();
        private readonly List<ListenerEntry> _pendingRemoves = new();

        public InMemorySignalBridge(ISignalBus bus)
        {
            _bus = bus;
        }

        // ═══════════════════════════════════════
        //  蓝图→外部（监听）
        // ═══════════════════════════════════════

        public IDisposable Listen(SignalTag tag, Action<SignalTag, SignalPayload> callback,
                                  SignalMatchMode mode = SignalMatchMode.Exact)
        {
            var entry = new ListenerEntry(tag, callback, mode);
            if (_dispatching)
                _pendingAdds.Add(entry);
            else
                _listeners.Add(entry);

            return new ListenerHandle(entry, this);
        }

        public IReadOnlyList<SignalEntry> GetEmittedThisTick() => _bus.GetFrameEmitted();

        // ═══════════════════════════════════════
        //  外部→蓝图（注入）
        // ═══════════════════════════════════════

        public void Send(SignalTag tag, SignalPayload? payload = null)
        {
            _bus.Inject(tag, payload);
        }

        public void Send(SignalTag tag, SignalPayload? payload, BlueprintEventContext? eventContext)
        {
            _bus.Inject(tag, payload, eventContext);
        }

        // ═══════════════════════════════════════
        //  帧末分发（由 Adapter 调用）
        // ═══════════════════════════════════════

        /// <summary>
        /// 在帧末（EndTick 后）调用，将本帧蓝图发射的信号分发给所有匹配的监听器。
        /// </summary>
        internal void DispatchEmitted()
        {
            var emitted = _bus.GetFrameEmitted();
            if (emitted.Count == 0) return;

            _dispatching = true;
            try
            {
                for (int i = 0; i < emitted.Count; i++)
                {
                    var sig = emitted[i];
                    for (int j = _listeners.Count - 1; j >= 0; j--)
                    {
                        var listener = _listeners[j];
                        if (listener.Matches(sig))
                        {
                            try
                            {
                                var tag = new SignalTag(
#if UNITY_EDITOR || DEBUG
                                    sig.DebugTag ?? sig.TagHash.ToString()
#else
                                    sig.TagHash.ToString()
#endif
                                );
                                listener.Callback(tag, SignalPayload.Empty);
                            }
                            catch (Exception e)
                            {
                                Debug.LogException(e);
                            }
                        }
                    }
                }
            }
            finally
            {
                _dispatching = false;
                FlushPending();
            }
        }

        // ═══════════════════════════════════════
        //  内部辅助
        // ═══════════════════════════════════════

        internal void RemoveListener(ListenerEntry entry)
        {
            if (_dispatching)
                _pendingRemoves.Add(entry);
            else
                _listeners.Remove(entry);
        }

        private void FlushPending()
        {
            if (_pendingRemoves.Count > 0)
            {
                foreach (var entry in _pendingRemoves)
                    _listeners.Remove(entry);
                _pendingRemoves.Clear();
            }

            if (_pendingAdds.Count > 0)
            {
                _listeners.AddRange(_pendingAdds);
                _pendingAdds.Clear();
            }
        }

        // ═══════════════════════════════════════
        //  监听器条目
        // ═══════════════════════════════════════

        internal class ListenerEntry
        {
            public readonly SignalTag Tag;
            public readonly Action<SignalTag, SignalPayload> Callback;
            public readonly SignalMatchMode Mode;

            public ListenerEntry(SignalTag tag, Action<SignalTag, SignalPayload> callback, SignalMatchMode mode)
            {
                Tag = tag;
                Callback = callback;
                Mode = mode;
            }

            public bool Matches(SignalEntry signal)
            {
                if (Mode == SignalMatchMode.Exact)
                {
                    return signal.TagHash == Tag.Path.GetHashCode();
                }

                // PrefixMatch：构造临时 SignalTag 检查前缀
#if UNITY_EDITOR || DEBUG
                if (signal.DebugTag != null)
                {
                    var signalTag = new SignalTag(signal.DebugTag);
                    return signalTag.HasPrefix(Tag.Path) ||
                           string.Equals(signalTag.Path, Tag.Path, StringComparison.Ordinal);
                }
#endif
                // Runtime 无 DebugTag 时，前缀匹配退化为精确匹配
                // （运行时只有 hash，无法做字符串前缀比较）
                return signal.TagHash == Tag.Path.GetHashCode();
            }
        }

        // ═══════════════════════════════════════
        //  监听句柄
        // ═══════════════════════════════════════

        private class ListenerHandle : IDisposable
        {
            private ListenerEntry? _entry;
            private InMemorySignalBridge? _bridge;

            public ListenerHandle(ListenerEntry entry, InMemorySignalBridge bridge)
            {
                _entry = entry;
                _bridge = bridge;
            }

            public void Dispose()
            {
                if (_entry != null && _bridge != null)
                {
                    _bridge.RemoveListener(_entry);
                    _entry = null;
                    _bridge = null;
                }
            }
        }
    }
}
