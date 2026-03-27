#nullable enable

namespace SceneBlueprint.Core
{
    /// <summary>
    /// ActionCompilationContext.UserData 的正式键名约定。
    /// 这些键用于把“蓝图所属关卡”等编译上下文从 editor 装配层稳定传递到业务 compiler。
    /// </summary>
    public static class ActionCompilationUserDataKeys
    {
        public const string LevelId = "levelId";

        public const string MonsterMappingSnapshot = "sceneBlueprint.monsterMapping";
    }
}
