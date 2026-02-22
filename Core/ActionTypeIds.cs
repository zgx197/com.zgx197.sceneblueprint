#nullable enable

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 所有内置 Action TypeId 的编译时常量，按分类嵌套组织。
    /// <para>
    /// System 层通过 <c>AT.xxx</c> 引用节点类型，避免魔法字符串：
    /// <code>
    /// var indices = frame.GetActionIndices(AT.Flow.Start);
    /// </code>
    /// </para>
    /// </summary>
    public static class AT
    {
        public static class Flow
        {
            public const string Start  = "Flow.Start";
            public const string End    = "Flow.End";
            public const string Filter = "Flow.Filter";
            public const string Branch = "Flow.Branch";
            public const string Join   = "Flow.Join";
            public const string Delay  = "Flow.Delay";
        }

        public static class Blackboard
        {
            public const string Get = "Blackboard.Get";
            public const string Set = "Blackboard.Set";
        }

        // Spawn.*、Trigger.*、Vfx.* 已迁移至 SceneBlueprintUser/Definitions/*.sbdef，
        // 由 SbdefCodeGen 自动生成到 SceneBlueprintUser/Generated/UAT.*.g.cs。
    }
}
