#nullable enable
using UnityEngine;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 进入区域条件等待系统——处理 Trigger.EnterArea 节点。
    /// <para>
    /// 统一为"条件等待节点"语义：
    /// - 被上游通过 in 端口激活后进入 Running
    /// - Running 阶段每帧检查玩家是否在触发区域内
    /// - 条件满足后标记 Completed，TransitionSystem 路由至 out 端口下游
    /// </para>
    /// <para>
    /// 当前实现为测试桩（Stub）：
    /// - 使用 CustomInt 作为等待计数器
    /// - 默认在第 1 个 Tick 即判定条件满足（模拟玩家已在区域内）
    /// - 后续迁移到 FrameSyncEngine 时，替换为真实的空间查询
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Business)]
    public class TriggerEnterAreaSystem : BlueprintSystemBase
    {
        public override string Name => "TriggerEnterAreaSystem";

        /// <summary>
        /// 玩家位置提供器（外部注入）。
        /// 为 null 时使用默认行为（立即满足条件，用于测试）。
        /// </summary>
        public IPlayerPositionProvider? PlayerPosition { get; set; }

        public override void Update(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Trigger.EnterArea);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref frame.States[idx];

                if (state.Phase != ActionPhase.Running)
                    continue;

                // 检查玩家是否在触发区域内
                bool isInArea = CheckPlayerInArea(frame, idx);

                if (isInArea)
                {
                    state.Phase = ActionPhase.Completed;
                    Debug.Log($"[TriggerEnterAreaSystem] Trigger.EnterArea (index={idx}) → Completed（玩家进入区域）");
                }
            }
        }

        /// <summary>
        /// 检查玩家是否在触发区域内。
        /// <para>
        /// 当前为测试桩实现：
        /// - 如果注入了 IPlayerPositionProvider，使用真实位置检测
        /// - 否则默认返回 true（模拟玩家已在区域内）
        /// </para>
        /// </summary>
        private bool CheckPlayerInArea(BlueprintFrame frame, int actionIndex)
        {
            if (PlayerPosition == null)
            {
                // 测试模式：默认条件满足
                return true;
            }

            // 获取触发区域的场景绑定数据
            var bindings = frame.GetSceneBindings(actionIndex);
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].BindingKey.Contains(ActionPortIds.TriggerEnterArea.TriggerArea))
                {
                    var playerPos = PlayerPosition.GetPosition();
                    // 简单的 AABB 检测（后续可替换为多边形检测）
                    var areaJson = bindings[i].SpatialPayloadJson;
                    if (!string.IsNullOrEmpty(areaJson) && areaJson != "{}")
                    {
                        return IsPointInArea(playerPos, areaJson);
                    }
                    // 无空间数据时默认满足
                    return true;
                }
            }

            // 未找到绑定时默认满足（避免阻塞流程）
            Debug.LogWarning($"[TriggerEnterAreaSystem] Trigger.EnterArea (index={actionIndex}) 未找到 triggerArea 绑定，默认满足条件");
            return true;
        }

        /// <summary>简单的点在矩形区域内检测（基于 SpatialPayload 中的 center/size）</summary>
        private static bool IsPointInArea(Vector3 point, string areaJson)
        {
            // 简化实现：尝试解析 center 和 size
            // 完整实现应使用多边形检测，与 SpawnWaveSystem 的区域解析对齐
            try
            {
                var payload = JsonUtility.FromJson<AreaCheckPayload>(areaJson);
                if (payload?.center != null && payload.size != null)
                {
                    var c = payload.center;
                    var s = payload.size;
                    float halfX = s.x * 0.5f;
                    float halfZ = s.z * 0.5f;
                    return point.x >= c.x - halfX && point.x <= c.x + halfX
                        && point.z >= c.z - halfZ && point.z <= c.z + halfZ;
                }
            }
            catch
            {
                // 解析失败时默认满足
            }
            return true;
        }

        /// <summary>区域检测用的简化 JSON 结构</summary>
        [System.Serializable]
        private class AreaCheckPayload
        {
            public Vec3Data? center;
            public Vec3Data? size;
        }

        [System.Serializable]
        private class Vec3Data
        {
            public float x, y, z;
        }
    }

    /// <summary>
    /// 玩家位置提供器接口。
    /// 运行时由游戏逻辑注入实现，测试时可 Mock。
    /// </summary>
    public interface IPlayerPositionProvider
    {
        Vector3 GetPosition();
    }
}
