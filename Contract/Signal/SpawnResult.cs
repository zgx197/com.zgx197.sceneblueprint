#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 刷怪结果——ISpawnHandler 完成创建后回报给蓝图的数据。
    /// <para>
    /// 包含外部系统创建的实体 ID，用于 EntityRegistry 建立蓝图逻辑引用与运行时实体的映射。
    /// </para>
    /// </summary>
    public class SpawnResult
    {
        /// <summary>是否创建成功</summary>
        public bool Success { get; private set; }

        /// <summary>外部系统创建的实体 ID（字符串形式，跨引擎兼容）</summary>
        public string EntityId { get; private set; } = "";

        /// <summary>失败原因（Success == false 时有效）</summary>
        public string ErrorMessage { get; private set; } = "";

        /// <summary>创建成功结果</summary>
        public static SpawnResult Ok(string entityId) => new SpawnResult { Success = true, EntityId = entityId };

        /// <summary>创建失败结果</summary>
        public static SpawnResult Fail(string errorMessage) => new SpawnResult { Success = false, ErrorMessage = errorMessage };

        public override string ToString()
            => Success ? $"Ok(EntityId={EntityId})" : $"Fail({ErrorMessage})";
    }
}
