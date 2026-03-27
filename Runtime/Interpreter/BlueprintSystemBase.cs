#nullable enable
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 蓝图 System 基类——处理特定类型 Action 节点的无状态逻辑处理器。
    /// <para>
    /// 对齐 FrameSyncEngine.SystemBase 的设计：
    /// - System 不持有可变状态，所有状态通过 FrameView 读写
    /// - 生命周期：OnInit → (Update 每帧循环) → OnDisabled
    /// - 每个 System 负责处理一类或多类 TypeId 的 Action
    /// </para>
    /// </summary>
    public abstract class BlueprintSystemBase
    {
        /// <summary>System 名称（用于日志和调试）</summary>
        public virtual string Name => GetType().Name;

        /// <summary>
        /// System 执行优先级（越小越先执行）。
        /// </summary>
        /// <remarks>
        /// 已由 <see cref="UpdateInGroupAttribute"/> + <see cref="UpdateAfterAttribute"/> 声明式属性取代。
        /// 保留此属性仅用于向后兼容——无 [UpdateInGroup] 标记的 System 仍以此值排序。
        /// 新增 System 请改用属性声明。
        /// </remarks>
        [System.Obsolete("请改用 [UpdateInGroup] + [UpdateAfter] 声明式属性替代 Order 数字。")]
        public virtual int Order => 100;

        /// <summary>是否启用（可动态开关）</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 初始化回调——蓝图加载完毕后调用一次。
        /// <para>用于预处理静态数据、构建内部索引等。</para>
        /// </summary>
        public virtual void OnInit(BlueprintFrame frame) { }

        /// <summary>
        /// 每帧更新回调——由 BlueprintRunner 在每次 Tick 中按 Order 顺序调用。
        /// <para>
        /// System 在此方法中扫描自己关心的 Action，根据 Phase 执行逻辑。
        /// 通过 FrameView 统一读写状态，保持与 mini_game 侧一致的数据访问模式。
        /// </para>
        /// </summary>
        public abstract void Update(ref FrameView view);

        /// <summary>
        /// 停用回调——蓝图执行结束或 Runner 销毁时调用。
        /// <para>用于清理临时资源。</para>
        /// </summary>
        public virtual void OnDisabled(BlueprintFrame frame) { }

        // ══════════════════════════════════════════
        //  端口事件发射辅助方法（通过 ITransitionRouter）
        // ══════════════════════════════════════════

        /// <summary>发射指定 flow 端口的转场事件（通过 FrameView.Router）</summary>
        protected static void EmitFlowEvent(ref FrameView view, int actionIndex, string portId)
            => view.Router.EmitFlowEvent(ref view, actionIndex, portId);

        /// <summary>发射 "out" 端口事件（通过 FrameView.Router）</summary>
        protected static void EmitOutEvent(ref FrameView view, int actionIndex)
            => view.Router.EmitOutEvent(ref view, actionIndex);
    }
}
