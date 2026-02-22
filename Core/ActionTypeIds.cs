#nullable enable

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 所有内置 Action TypeId 的编译时常量，按分类嵌套组织。
    /// <para>
    /// System 层通过 <c>AT.xxx</c> 引用节点类型，避免魔法字符串：
    /// <code>
    /// var indices = frame.GetActionIndices(AT.Spawn.Wave);
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

        public static class Spawn
        {
            public const string Wave   = "Spawn.Wave";
            public const string Preset = "Spawn.Preset";
        }

        public static class Trigger
        {
            public const string EnterArea = "Trigger.EnterArea";
        }

        public static class Blackboard
        {
            public const string Get = "Blackboard.Get";
            public const string Set = "Blackboard.Set";
        }

        public static class Vfx
        {
            public const string CameraShake = "VFX.CameraShake";
            public const string ScreenFlash = "VFX.ScreenFlash";
            public const string ShowWarning = "VFX.ShowWarning";
        }
    }
}
