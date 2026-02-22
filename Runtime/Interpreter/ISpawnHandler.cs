#nullable enable
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 刷怪数据（由 SpawnPresetSystem 解析后传递给外部处理器）。
    /// </summary>
    public struct SpawnData
    {
        public int Index;
        public string MonsterId;
        public int Level;
        public string Behavior;
        public float GuardRadius;
        public Vector3 Position;
        public Vector3 EulerRotation;
    }

    /// <summary>
    /// 刷怪处理器接口——运行时解释器与可视化层的桥梁。
    /// <para>
    /// SpawnPresetSystem 解析绑定数据后，通过此接口通知外部创建实体。
    /// 不同环境提供不同实现：
    /// - 编辑器测试：创建彩色 Cube（MonsterSpawner）
    /// - 帧同步运行时：创建 ECS Entity
    /// </para>
    /// </summary>
    public interface ISpawnHandler
    {
        /// <summary>单个怪物生成回调</summary>
        void OnSpawn(SpawnData data);

        /// <summary>一批怪物生成完毕回调（可选，用于批量后处理）</summary>
        void OnSpawnBatchComplete(int totalCount) { }
    }
}
