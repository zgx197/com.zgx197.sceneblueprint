#nullable enable

namespace SceneBlueprint.Runtime.Interpreter.Diagnostics
{
    /// <summary>
    /// 单帧内某个 Action 的 Phase 变化记录（值类型，零GC）。
    /// </summary>
    public readonly struct StateDiff
    {
        /// <summary>发生变化的 ActionIndex</summary>
        public readonly int ActionIndex;

        /// <summary>变化前的阶段</summary>
        public readonly ActionPhase PhaseBefore;

        /// <summary>变化后的阶段</summary>
        public readonly ActionPhase PhaseAfter;

        public StateDiff(int actionIndex, ActionPhase phaseBefore, ActionPhase phaseAfter)
        {
            ActionIndex  = actionIndex;
            PhaseBefore  = phaseBefore;
            PhaseAfter   = phaseAfter;
        }

        public override string ToString()
            => $"[{ActionIndex}] {PhaseBefore} → {PhaseAfter}";
    }
}
