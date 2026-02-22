#nullable enable
using SceneBlueprint.Contract;
using UnityEngine;
using SceneBlueprint.Core;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 预设刷怪系统——处理 Spawn.Preset 节点的运行时执行。
    /// <para>
    /// Phase 1 实现（日志验证）：
    /// - 读取 SceneBindings 中的子 PointMarker 位置 + Annotation 数据
    /// - 打印刷怪日志（怪物类型、位置、行为）
    /// - 立即标记 Completed
    /// </para>
    /// <para>
    /// Phase 4（迁移到帧同步）：
    /// - 调用 Frame.Create&lt;MonsterEntity&gt;() 创建实体
    /// - 设置 Transform / AI / Stats 等 Component
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Business)]
    public class SpawnPresetSystem : BlueprintSystemBase
    {
        public override string Name => "SpawnPresetSystem";

        /// <summary>外部刷怪处理器（可选，未设置时仅打印日志）</summary>
        public ISpawnHandler? SpawnHandler { get; set; }

        public override void Update(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Spawn.Preset);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref frame.States[idx];

                if (state.Phase != ActionPhase.Running)
                    continue;

                ExecuteSpawnPreset(frame, idx);

                // 瞬时型节点——执行完毕立即 Completed
                state.Phase = ActionPhase.Completed;
            }
        }

        /// <summary>
        /// 执行预设刷怪逻辑。
        /// Phase 1：解析数据并打印日志；Phase 4：实际创建实体。
        /// </summary>
        private void ExecuteSpawnPreset(BlueprintFrame frame, int actionIndex)
        {
            var bindings = frame.GetSceneBindings(actionIndex);

            Debug.Log($"[SpawnPresetSystem] ═══ 执行 Spawn.Preset (index={actionIndex}) ═══");
            Debug.Log($"[SpawnPresetSystem] 场景绑定数量: {bindings.Length}");

            int spawnCount = 0;

            for (int b = 0; b < bindings.Length; b++)
            {
                var binding = bindings[b];

                // 解析空间数据（位置/朝向）
                var spatialData = ParseSpatialPayload(binding.SpatialPayloadJson);

                // 解析 Annotation 数据（怪物信息）
                var spawnInfo = ExtractSpawnAnnotation(binding.Annotations);

                if (spawnInfo.HasData)
                {
                    Debug.Log(
                        $"[SpawnPresetSystem]   [{spawnCount}] 怪物={spawnInfo.MonsterId}, " +
                        $"等级={spawnInfo.Level}, 行为={spawnInfo.Behavior}, " +
                        $"位置=({spatialData.PosX:F1}, {spatialData.PosY:F1}, {spatialData.PosZ:F1}), " +
                        $"朝向=({spatialData.RotX:F1}, {spatialData.RotY:F1}, {spatialData.RotZ:F1})");

                    // 通过 ISpawnHandler 回调通知外部创建实体
                    SpawnHandler?.OnSpawn(new SpawnData
                    {
                        Index = spawnCount,
                        MonsterId = spawnInfo.MonsterId,
                        Level = spawnInfo.Level,
                        Behavior = spawnInfo.Behavior,
                        GuardRadius = spawnInfo.GuardRadius,
                        Position = new Vector3(spatialData.PosX, spatialData.PosY, spatialData.PosZ),
                        EulerRotation = new Vector3(spatialData.RotX, spatialData.RotY, spatialData.RotZ)
                    });

                    spawnCount++;
                }
                else
                {
                    Debug.Log(
                        $"[SpawnPresetSystem]   [{b}] 点位（无 Spawn 标注）: " +
                        $"位置=({spatialData.PosX:F1}, {spatialData.PosY:F1}, {spatialData.PosZ:F1})");
                }
            }

            Debug.Log($"[SpawnPresetSystem] ═══ 刷怪完成: 共 {spawnCount} 个怪物 ═══");

            // 通知批量完成
            SpawnHandler?.OnSpawnBatchComplete(spawnCount);
        }

        // ── 数据解析辅助 ──

        /// <summary>空间数据（从 SpatialPayloadJson 解析）</summary>
        private struct SpatialData
        {
            public float PosX, PosY, PosZ;
            public float RotX, RotY, RotZ;
        }

        /// <summary>刷怪标注数据（从 Annotations 提取）</summary>
        private struct SpawnInfo
        {
            public bool HasData;
            public string MonsterId;
            public int Level;
            public string Behavior;
            public float GuardRadius;
        }

        /// <summary>解析 SpatialPayloadJson（{"position":{"x":..},"rotation":{"x":..}}）</summary>
        private static SpatialData ParseSpatialPayload(string? json)
        {
            var data = new SpatialData();
            if (string.IsNullOrEmpty(json)) return data;

            // 使用 Unity JsonUtility 解析嵌套结构
            try
            {
                var payload = JsonUtility.FromJson<SpatialPayloadJson>(json);
                if (payload != null)
                {
                    data.PosX = payload.position.x;
                    data.PosY = payload.position.y;
                    data.PosZ = payload.position.z;
                    data.RotX = payload.rotation.x;
                    data.RotY = payload.rotation.y;
                    data.RotZ = payload.rotation.z;
                }
            }
            catch
            {
                // 解析失败时保持默认值
            }

            return data;
        }

        /// <summary>从 Annotations 数组中提取 Spawn 类型的标注</summary>
        private static SpawnInfo ExtractSpawnAnnotation(AnnotationDataEntry[]? annotations)
        {
            var info = new SpawnInfo();
            if (annotations == null) return info;

            for (int i = 0; i < annotations.Length; i++)
            {
                if (annotations[i].TypeId != "Spawn") continue; // Annotation TypeId，非 Action TypeId

                info.HasData = true;
                var props = annotations[i].Properties;
                for (int p = 0; p < props.Length; p++)
                {
                    switch (props[p].Key)
                    {
                        case "monsterId":
                            info.MonsterId = props[p].Value ?? "";
                            break;
                        case "level":
                            int.TryParse(props[p].Value, out info.Level);
                            break;
                        case "behavior":
                            info.Behavior = props[p].Value ?? "";
                            break;
                        case "guardRadius":
                            float.TryParse(props[p].Value, out info.GuardRadius);
                            break;
                    }
                }
                break; // 只取第一个 Spawn 标注
            }

            return info;
        }

        // ── JSON 反序列化辅助类型 ──

        [System.Serializable]
        private class SpatialPayloadJson
        {
            public Vec3Json position = new();
            public Vec3Json rotation = new();
        }

        [System.Serializable]
        private class Vec3Json
        {
            public float x, y, z;
        }
    }
}
