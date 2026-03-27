#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.Interpreter;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Adapters
{
    /// <summary>
    /// ISignalBus 的 Package 侧实现——纯 C# 堆内存队列，适用于 Digong/普通 Unity 运行时。
    /// <para>
    /// 帧级快照模式：
    /// - OnBeginTick 清空上一帧的数据
    /// - System 在 Tick 中写入信号
    /// - OnEndTick 清理帧级缓冲
    /// </para>
    /// </summary>
    public class InMemorySignalBus : ISignalBus, IBlueprintEventHistorySignalBus
    {
        // 帧级缓冲
        private readonly List<SignalEntry> _emitted = new();
        private readonly List<SignalEntry> _injected = new();
        private readonly List<SignalEntry> _pendingInjected = new();
        private readonly HashSet<ConditionWatchHandle> _triggeredConditions = new();
        private readonly HashSet<ConditionWatchHandle> _pendingTriggeredConditions = new();

        // 条件评估器注册表
        private readonly Dictionary<string, IConditionEvaluator> _evaluators = new();

        // 活跃的条件监听（watchHandle → registration）
        private readonly Dictionary<ConditionWatchHandle, ConditionWatchRegistration> _activeWatches = new();

        private int _currentTick;
        private bool _isInsideTick;

        public IBlueprintEventHistoryRecorder? EventHistoryRecorder { get; set; }

        // ═══════════════════════════════════════
        //  信号发射/注入
        // ═══════════════════════════════════════

        public void Emit(SignalTag tag, SignalPayload? payload, BlueprintEventContext? eventContext = null)
        {
            var normalizedEventContext = BlueprintEventContextSemanticUtility.Normalize(eventContext);
            var entry = new SignalEntry
            {
                TagHash = tag.Path.GetHashCode(),
                ActionIndex = -1,
                PayloadInt = 0,
                SubjectRefSerialized = normalizedEventContext?.SubjectRefSerialized ?? string.Empty,
                EventContext = normalizedEventContext,
#if UNITY_EDITOR || DEBUG
                DebugTag = tag.Path,
#endif
            };
            _emitted.Add(entry);
            Debug.Log($"[InMemorySignalBus] Emit: {tag.Path}");
        }

        public IReadOnlyList<SignalEntry> GetFrameEmitted() => _emitted;

        public void Inject(SignalTag tag, SignalPayload? payload, BlueprintEventContext? eventContext = null)
        {
            var normalizedEventContext = BlueprintEventContextSemanticUtility.Normalize(eventContext);
            var historyContext = BlueprintEventContextSemanticUtility.CreateInjectedContext(
                tag,
                payload,
                normalizedEventContext,
                _currentTick);
            var entry = new SignalEntry
            {
                TagHash = tag.Path.GetHashCode(),
                ActionIndex = -1,
                PayloadInt = 0,
                SubjectRefSerialized = normalizedEventContext?.SubjectRefSerialized ?? string.Empty,
                EventContext = historyContext,
#if UNITY_EDITOR || DEBUG
                DebugTag = tag.Path,
#endif
            };

            if (_isInsideTick)
            {
                _injected.Add(entry);
            }
            else
            {
                _pendingInjected.Add(entry);
            }

            EventHistoryRecorder?.RecordInjected(historyContext);
        }

        public IReadOnlyList<SignalEntry> GetFrameInjected() => _injected;

        // ═══════════════════════════════════════
        //  条件监听
        // ═══════════════════════════════════════

        public ConditionWatchHandle BeginConditionWatch(ConditionWatchRegistration registration)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            _activeWatches[registration.Handle] = registration;

            if (_evaluators.TryGetValue(registration.ConditionType, out var evaluator))
            {
                evaluator.BeginWatch(registration, this);
                Debug.Log(
                    $"[InMemorySignalBus] BeginWatch: handle={registration.Handle}, " +
                    $"target={registration.Descriptor.Target}");
            }
            else
            {
                Debug.LogWarning(
                    $"[InMemorySignalBus] 未找到 Evaluator: {registration.ConditionType}，handle={registration.Handle}");
            }

            return registration.Handle;
        }

        public void EndConditionWatch(ConditionWatchHandle watchHandle)
        {
            if (_activeWatches.TryGetValue(watchHandle, out var registration))
            {
                _activeWatches.Remove(watchHandle);
                if (_evaluators.TryGetValue(registration.ConditionType, out var evaluator))
                {
                    evaluator.EndWatch(registration.Handle);
                }
            }

            _triggeredConditions.Remove(watchHandle);
        }

        public bool IsConditionTriggered(ConditionWatchHandle watchHandle)
            => _triggeredConditions.Contains(watchHandle);

        public void TriggerCondition(ConditionWatchHandle watchHandle)
        {
            if (_isInsideTick)
            {
                _triggeredConditions.Add(watchHandle);
            }
            else
            {
                _pendingTriggeredConditions.Add(watchHandle);
            }

            Debug.Log($"[InMemorySignalBus] TriggerCondition: handle={watchHandle}");
        }

        // ═══════════════════════════════════════
        //  评估器注册
        // ═══════════════════════════════════════

        public void RegisterEvaluator(IConditionEvaluator evaluator)
        {
            _evaluators[evaluator.TypeId] = evaluator;
        }

        // ═══════════════════════════════════════
        //  生命周期
        // ═══════════════════════════════════════

        public void OnBeginTick(int currentTick)
        {
            _currentTick = currentTick;
            _isInsideTick = true;
            _emitted.Clear();
            _injected.Clear();
            _triggeredConditions.Clear();

            for (var index = 0; index < _pendingInjected.Count; index++)
            {
                _injected.Add(_pendingInjected[index]);
            }

            _pendingInjected.Clear();

            foreach (var watchHandle in _pendingTriggeredConditions)
            {
                _triggeredConditions.Add(watchHandle);
            }

            _pendingTriggeredConditions.Clear();
        }

        public void OnEndTick()
        {
            _isInsideTick = false;
        }

        public void Dispose()
        {
            // 清理所有活跃的条件监听
            foreach (var kv in _activeWatches)
            {
                if (_evaluators.TryGetValue(kv.Value.ConditionType, out var evaluator))
                {
                    evaluator.EndWatch(kv.Value.Handle);
                }
            }
            _activeWatches.Clear();
            _evaluators.Clear();
            _emitted.Clear();
            _injected.Clear();
            _pendingInjected.Clear();
            _triggeredConditions.Clear();
            _pendingTriggeredConditions.Clear();
            _isInsideTick = false;
        }
    }
}
