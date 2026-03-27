#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.Interpreter;

namespace SceneBlueprint.Runtime.Interpreter.Adapters
{
    /// <summary>
    /// Package 侧帧适配器——从 BlueprintFrame 创建 FrameView（零拷贝直接引用）。
    /// <para>
    /// 设计原则：
    /// - BeginTick：将 BlueprintFrame 的数据投影为 FrameView（States 直接引用，零拷贝）
    /// - EndTick：回写（对于 class-based 存储，States 已是直接引用，无需额外回写）
    /// </para>
    /// <para>
    /// 与 mini_game 侧 FSFrameAdapter 的区别：
    /// - Class 版零拷贝（直接引用 BlueprintFrame.States 数组）
    /// - FS 版需要 bulk copy（qtn Component → ActionRuntimeState[]）
    /// </para>
    /// </summary>
    public class ClassFrameAdapter
    {
        private readonly ClassActionQuery _query;
        private readonly ClassTransitionRouter _router;
        private ISignalBus? _bus;
        private InMemorySignalBridge? _bridge;

        public ClassFrameAdapter()
        {
            // 使用占位 frame，Load 时通过 SetFrame 设置真实 frame
            var placeholder = new BlueprintFrame();
            _query = new ClassActionQuery(placeholder);
            _router = new ClassTransitionRouter(placeholder);
        }

        /// <summary>设置信号总线（可选）</summary>
        public void SetSignalBus(ISignalBus? bus) => _bus = bus;

        /// <summary>设置信号桥接器（可选，用于帧末分发）</summary>
        internal void SetSignalBridge(InMemorySignalBridge? bridge) => _bridge = bridge;

        /// <summary>切换到新 Frame（BlueprintRunner.Load 时调用）</summary>
        public void SetFrame(BlueprintFrame frame)
        {
            _query.SetFrame(frame);
            _router.SetFrame(frame);
        }

        /// <summary>获取 Query 实例（用于 OnInit 等需要直接访问的场景）</summary>
        public ClassActionQuery Query => _query;

        /// <summary>
        /// 帧开始——创建 FrameView。
        /// <para>
        /// States 和 PendingEvents 均直接引用 BlueprintFrame 内部实例（零拷贝）。
        /// 不在此处清空 PendingEvents——由 TransitionSystem 消费后清空。
        /// 这保证了上一帧发射的事件能在本帧被 TransitionSystem 消费。
        /// </para>
        /// </summary>
        public FrameView BeginTick(BlueprintFrame frame)
        {
            _bus?.OnBeginTick(frame.TickCount);

            // 在帧开始时读取一次统一时间配置快照。
            // 这样本帧内所有 System 都基于同一份配置执行，避免热路径反复读取全局 ScriptableObject，
            // 同时也为后续 FrameSync 侧“初始化时冻结配置快照”提供相同的调用模型。
            var timeSettings = BlueprintRuntimeSettings.Instance.TimeSettings;

            return new FrameView
            {
                States = frame.States,
                PendingEvents = frame.PendingEvents, // 直接引用 frame 的列表，统一事件来源
                ActionCount = frame.ActionCount,
                CurrentTick = frame.TickCount,
                // 兼容字段：保留给尚未迁移到 TimeSettings 的旧系统或业务层使用。
                TargetTickRate = timeSettings.TargetTickRate,
                // 新标准入口：所有新的时间换算与 deadline 判断都应读取这个快照。
                TimeSettings = timeSettings,
                Query = _query,
                Router = _router,
                Bus = _bus,
            };
        }

        /// <summary>
        /// 帧结束——回写（class-based 零拷贝场景下，States 已是直接引用无需回写）。
        /// </summary>
        public void EndTick(BlueprintFrame frame, ref FrameView view)
        {
            _bus?.OnEndTick();

            // 帧末分发：将本帧蓝图发射的信号通知给所有外部监听器
            _bridge?.DispatchEmitted();

            // States / PendingEvents 都是直接引用，已经被 System 原地修改
        }
    }
}
