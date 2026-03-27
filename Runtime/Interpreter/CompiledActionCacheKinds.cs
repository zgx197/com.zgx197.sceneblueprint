#nullable enable

namespace SceneBlueprint.Runtime.Interpreter
{
    internal static class CompiledActionCacheKinds
    {
        public const string SignalAction = "compiled.signal.action";
        public const string FlowAction = "compiled.flow.action";
        public const string BlackboardAction = "compiled.blackboard.action";
        public const string EntityRefAction = "compiled.entity-ref-action";
        public const string VfxAction = "compiled.vfx.action";
        public const string SpawnPresetPlan = "compiled.spawn-preset.plan";
        public const string SpawnWavePlan = "compiled.spawn-wave.plan";
    }
}
