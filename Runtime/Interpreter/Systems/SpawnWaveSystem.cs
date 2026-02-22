#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Contract;
using UnityEngine;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 波次刷怪系统——处理 Spawn.Wave 节点的运行时执行。
    /// <para>
    /// 职责拆分后的设计：
    /// - 怪物池数据来自 SceneBinding.Annotations（WaveSpawnConfig 导出）
    /// - 波次配置来自 Properties["waves"]（Spawn.Wave 节点属性）
    /// - 运行时按波次配置，从怪物池中按标签筛选、按权重随机抽取
    /// </para>
    /// <para>
    /// 状态管理：
    ///   CustomInt   → 当前波次索引（0-based）
    ///   CustomFloat → 上次刷怪的 Tick 时间戳
    /// </para>
    /// <para>
    /// 每波生成流程：
    /// 1. 解析区域几何（center + size + rotation）
    /// 2. 解析 WaveSpawnConfig 怪物池（从 Annotations）
    /// 3. 解析波次配置列表（从 Properties["waves"]）
    /// 4. 触发 onWaveStart 端口事件（不阻塞刷怪）
    /// 5. 根据 monsterFilter 筛选候选怪物
    /// 6. 按 weight 权重随机抽取 count 个怪物
    /// 7. 在区域内用拒绝采样生成随机位置（满足 MinSpacing）
    /// 8. 通过 ISpawnHandler 回调创建怪物
    /// 9. 递增波次索引，全部完成 → Completed
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Business)]
    [UpdateAfter(typeof(SpawnPresetSystem))]
    public class SpawnWaveSystem : BlueprintSystemBase
    {
        public override string Name => "SpawnWaveSystem";

        public ISpawnHandler? SpawnHandler { get; set; }

        private const int MaxSamplingAttempts = 100;

        public override void Update(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Spawn.Wave);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref frame.States[idx];

                if (state.Phase != ActionPhase.Running)
                    continue;

                ProcessWaveAction(frame, idx, ref state);
            }
        }

        private void ProcessWaveAction(BlueprintFrame frame, int actionIndex, ref ActionRuntimeState state)
        {
            var bindings = frame.GetSceneBindings(actionIndex);
            if (bindings.Length == 0)
            {
                Debug.LogWarning($"[SpawnWaveSystem] Spawn.Wave (index={actionIndex}) 无 SceneBinding，标记 Completed");
                state.Phase = ActionPhase.Completed;
                return;
            }

            // 解析配置（仅第一个绑定）
            var binding = bindings[0];
            var areaData = ParseAreaPayload(binding.SpatialPayloadJson);
            var monsterPool = ExtractMonsterPool(binding.Annotations);

            if (monsterPool.Length == 0)
            {
                Debug.LogWarning($"[SpawnWaveSystem] Spawn.Wave (index={actionIndex}) 无怪物池数据，标记 Completed");
                state.Phase = ActionPhase.Completed;
                return;
            }

            // 解析波次配置
            var waveEntries = ParseWaveEntries(frame, actionIndex);
            if (waveEntries.Length == 0)
            {
                Debug.LogWarning($"[SpawnWaveSystem] Spawn.Wave (index={actionIndex}) 无波次配置，标记 Completed");
                state.Phase = ActionPhase.Completed;
                return;
            }

            // 解析空间设置
            float minSpacing = ExtractMinSpacing(binding.Annotations);

            int currentWave = state.CustomInt;   // 当前波次索引（0-based）
            int lastSpawnTick = (int)state.CustomFloat; // 上次刷怪 Tick

            // 全部波次已完成
            if (currentWave >= waveEntries.Length)
            {
                state.Phase = ActionPhase.Completed;
                Debug.Log($"[SpawnWaveSystem] ═══ 所有波次完成 (index={actionIndex}) ═══");
                return;
            }

            var currentEntry = waveEntries[currentWave];

            // 判断是否该刷下一波
            bool isFirstWave = (currentWave == 0 && state.CustomFloat == 0f);
            bool intervalElapsed = (frame.TickCount - lastSpawnTick) >= currentEntry.intervalTicks;

            if (isFirstWave || intervalElapsed)
            {
                // 写入数据端口值（供连接了 DataIn 的下游节点读取）
                frame.SetDataPortValue(actionIndex, ActionPortIds.SpawnWave.WaveIndex,  state.CustomInt.ToString());
                frame.SetDataPortValue(actionIndex, ActionPortIds.SpawnWave.TotalWaves, waveEntries.Length.ToString());

                // 触发 onWaveStart 端口事件（不阻塞刷怪）
                EmitWaveStartEvent(frame, actionIndex, currentWave);

                // 筛选候选怪物
                var candidates = FilterMonsters(monsterPool, currentEntry.monsterFilter);
                if (candidates.Length == 0)
                {
                    // 回退到全部怪物池
                    Debug.LogWarning($"[SpawnWaveSystem] 波次 {currentWave + 1}: 筛选标签 '{currentEntry.monsterFilter}' " +
                                     $"无匹配怪物，回退到全部怪物池");
                    candidates = monsterPool;
                }

                // 按权重随机抽取
                var selectedMonsters = WeightedSample(candidates, currentEntry.count);

                // 在区域内生成随机位置
                var positions = GenerateRandomPositions(areaData, currentEntry.count, minSpacing);

                Debug.Log($"[SpawnWaveSystem] ── 波次 {currentWave + 1}/{waveEntries.Length}: " +
                          $"生成 {currentEntry.count} 个怪物 (筛选={currentEntry.monsterFilter}, " +
                          $"间隔={currentEntry.intervalTicks} Tick) ──");

                // 生成怪物
                for (int i = 0; i < selectedMonsters.Length && i < positions.Count; i++)
                {
                    var monster = selectedMonsters[i];
                    var pos = positions[i];
                    var rot = new Vector3(0, Random.Range(0f, 360f), 0);

                    Debug.Log($"[SpawnWaveSystem]   [{i}] 怪物={monster.monsterId}, " +
                              $"等级={monster.level}, 行为={monster.behavior}, " +
                              $"标签={monster.tag}, " +
                              $"位置=({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");

                    SpawnHandler?.OnSpawn(new SpawnData
                    {
                        Index = i,
                        MonsterId = monster.monsterId,
                        Level = monster.level,
                        Behavior = monster.behavior,
                        GuardRadius = monster.guardRadius,
                        Position = pos,
                        EulerRotation = rot
                    });
                }

                SpawnHandler?.OnSpawnBatchComplete(Mathf.Min(selectedMonsters.Length, positions.Count));

                // 递增波次索引，记录当前 Tick
                currentWave++;
                state.CustomInt = currentWave;
                state.CustomFloat = frame.TickCount;

                Debug.Log($"[SpawnWaveSystem] 波次 {currentWave}/{waveEntries.Length} 生成完毕 " +
                          $"(Tick={frame.TickCount}, Action index={actionIndex})");

                // 全部波次完成
                if (currentWave >= waveEntries.Length)
                {
                    state.Phase = ActionPhase.Completed;
                    Debug.Log($"[SpawnWaveSystem] ═══ 所有波次完成 (index={actionIndex}) ═══");
                }
            }
        }

        // ── 波次配置解析 ──

        /// <summary>
        /// 从 Properties["waves"] 解析波次配置列表。
        /// 兜底：如果没有波次配置，生成默认的单波次。
        /// </summary>
        private static WaveEntryRuntime[] ParseWaveEntries(BlueprintFrame frame, int actionIndex)
        {
            var wavesJson = frame.GetProperty(actionIndex, ActionPortIds.SpawnWave.Waves);
            if (string.IsNullOrEmpty(wavesJson) || wavesJson == "[]")
            {
                // 兜底：默认单波次，5 个怪物，立即开始，使用全部怪物池
                Debug.Log($"[SpawnWaveSystem] 无波次配置，使用默认单波次 (count=5, filter=All)");
                return new[]
                {
                    new WaveEntryRuntime { count = 5, intervalTicks = 0, monsterFilter = "All" }
                };
            }

            try
            {
                // JsonUtility 需要包装器（顶层不能是数组）
                var wrapped = $"{{\"items\":{wavesJson}}}";
                var wrapper = JsonUtility.FromJson<WaveEntryListJson>(wrapped);
                if (wrapper?.items == null || wrapper.items.Length == 0)
                {
                    return new[]
                    {
                        new WaveEntryRuntime { count = 5, intervalTicks = 0, monsterFilter = "All" }
                    };
                }

                var result = new WaveEntryRuntime[wrapper.items.Length];
                for (int i = 0; i < wrapper.items.Length; i++)
                {
                    var src = wrapper.items[i];
                    result[i] = new WaveEntryRuntime
                    {
                        count = Mathf.Max(1, src.count),
                        intervalTicks = Mathf.Max(0, src.intervalTicks),
                        monsterFilter = string.IsNullOrEmpty(src.monsterFilter) ? "All" : src.monsterFilter
                    };
                }
                return result;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SpawnWaveSystem] 解析波次配置失败: {e.Message}, JSON: {wavesJson}");
                return new[]
                {
                    new WaveEntryRuntime { count = 5, intervalTicks = 0, monsterFilter = "All" }
                };
            }
        }

        // ── 怪物筛选 ──

        /// <summary>
        /// 根据筛选标签从怪物池中获取候选怪物。
        /// 空筛选或 "All" → 返回全部怪物池。
        /// </summary>
        private static MonsterInfo[] FilterMonsters(MonsterInfo[] pool, string filter)
        {
            if (string.IsNullOrEmpty(filter) || filter == "All")
                return pool;

            // 按标签筛选
            var filtered = new List<MonsterInfo>();
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].tag == filter)
                    filtered.Add(pool[i]);
            }
            return filtered.Count > 0 ? filtered.ToArray() : System.Array.Empty<MonsterInfo>();
        }

        // ── 权重随机抽取 ──

        /// <summary>
        /// 按权重从候选怪物中随机抽取 count 个（有放回抽样）。
        /// 同一个怪物可以被多次抽中，权重越高出现概率越大。
        /// </summary>
        private static MonsterInfo[] WeightedSample(MonsterInfo[] candidates, int count)
        {
            if (candidates.Length == 0) return System.Array.Empty<MonsterInfo>();
            if (candidates.Length == 1)
            {
                // 只有一种怪物，直接填充
                var single = new MonsterInfo[count];
                for (int i = 0; i < count; i++) single[i] = candidates[0];
                return single;
            }

            // 计算总权重
            int totalWeight = 0;
            for (int i = 0; i < candidates.Length; i++)
                totalWeight += Mathf.Max(1, candidates[i].weight);

            var result = new MonsterInfo[count];
            for (int i = 0; i < count; i++)
            {
                int roll = Random.Range(0, totalWeight);
                int cumulative = 0;
                for (int j = 0; j < candidates.Length; j++)
                {
                    cumulative += Mathf.Max(1, candidates[j].weight);
                    if (roll < cumulative)
                    {
                        result[i] = candidates[j];
                        break;
                    }
                }
            }
            return result;
        }

        // ── onWaveStart 端口事件 ──

        /// <summary>
        /// 触发 onWaveStart 端口事件。
        /// 不阻塞刷怪——先触发事件，再生成怪物。
        /// 如果 onWaveStart 没有连接下游节点，不产生任何事件。
        /// </summary>
        private static void EmitWaveStartEvent(BlueprintFrame frame, int actionIndex, int waveIndex)
        {
            var transitionIndices = frame.GetOutgoingTransitionIndices(actionIndex);
            bool emitted = false;
            for (int t = 0; t < transitionIndices.Count; t++)
            {
                var transition = frame.Transitions[transitionIndices[t]];
                if (transition.FromPortId == ActionPortIds.SpawnWave.OnWaveStart)
                {
                    var toIndex = frame.GetActionIndex(transition.ToActionId);
                    if (toIndex >= 0)
                    {
                        frame.PendingEvents.Add(new PortEvent(
                            actionIndex, ActionPortIds.SpawnWave.OnWaveStart, toIndex, transition.ToPortId));
                        emitted = true;
                    }
                }
            }

            if (emitted)
            {
                Debug.Log($"[SpawnWaveSystem] 触发 onWaveStart 事件 (waveIndex={waveIndex})");
            }
        }

        // ── 区域随机位置生成 ──

        /// <summary>在区域内用拒绝采样生成随机位置</summary>
        private List<Vector3> GenerateRandomPositions(AreaPayload area, int count, float minSpacing)
        {
            var result = new List<Vector3>(count);
            var halfSize = area.Size * 0.5f;
            var rotation = Quaternion.Euler(area.Rotation);
            float minSqr = minSpacing * minSpacing;

            for (int i = 0; i < count; i++)
            {
                bool placed = false;
                for (int attempt = 0; attempt < MaxSamplingAttempts; attempt++)
                {
                    // 在局部空间随机采样
                    var localPos = new Vector3(
                        Random.Range(-halfSize.x, halfSize.x),
                        0,
                        Random.Range(-halfSize.z, halfSize.z)
                    );

                    // 转换到世界空间
                    var worldPos = area.Center + rotation * localPos;

                    // 检查最小间距
                    bool tooClose = false;
                    for (int j = 0; j < result.Count; j++)
                    {
                        if ((result[j] - worldPos).sqrMagnitude < minSqr)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        result.Add(worldPos);
                        placed = true;
                        break;
                    }
                }

                // 采样失败时强制放置（降级）
                if (!placed)
                {
                    var fallback = area.Center + rotation * new Vector3(
                        Random.Range(-halfSize.x, halfSize.x),
                        0,
                        Random.Range(-halfSize.z, halfSize.z)
                    );
                    result.Add(fallback);
                }
            }

            return result;
        }

        // ── 数据解析 ──

        private struct AreaPayload
        {
            public Vector3 Center;
            public Vector3 Rotation;
            public Vector3 Size;
            public string Shape;
        }

        /// <summary>运行时怪物信息（从 WaveSpawnConfig 导出数据解析）</summary>
        private struct MonsterInfo
        {
            public string monsterId;
            public int level;
            public string behavior;
            public float guardRadius;
            public string tag;    // MonsterTag 枚举的字符串表示
            public int weight;
        }

        /// <summary>运行时波次配置（从 Properties["waves"] 解析）</summary>
        private struct WaveEntryRuntime
        {
            public int count;
            public int intervalTicks;
            public string monsterFilter;
        }

        /// <summary>从 Annotations 中提取怪物池</summary>
        private static MonsterInfo[] ExtractMonsterPool(AnnotationDataEntry[]? annotations)
        {
            if (annotations == null) return System.Array.Empty<MonsterInfo>();

            for (int i = 0; i < annotations.Length; i++)
            {
                if (annotations[i].TypeId != "WaveSpawn") continue;

                var props = annotations[i].Properties;
                for (int p = 0; p < props.Length; p++)
                {
                    if (props[p].Key == "monsters")
                        return ParseMonsterList(props[p].Value);
                }
                break;
            }

            return System.Array.Empty<MonsterInfo>();
        }

        /// <summary>从 Annotations 中提取最小间距</summary>
        private static float ExtractMinSpacing(AnnotationDataEntry[]? annotations)
        {
            if (annotations == null) return 1.5f;

            for (int i = 0; i < annotations.Length; i++)
            {
                if (annotations[i].TypeId != "WaveSpawn") continue;

                var props = annotations[i].Properties;
                for (int p = 0; p < props.Length; p++)
                {
                    if (props[p].Key == "minSpacing")
                    {
                        if (float.TryParse(props[p].Value,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float spacing))
                            return spacing;
                    }
                }
                break;
            }

            return 1.5f;
        }

        private static AreaPayload ParseAreaPayload(string? json)
        {
            var data = new AreaPayload { Size = new Vector3(10, 0, 10), Shape = "Box" };
            if (string.IsNullOrEmpty(json)) return data;

            try
            {
                var payload = JsonUtility.FromJson<AreaPayloadJson>(json);
                if (payload != null)
                {
                    data.Center = new Vector3(payload.center.x, payload.center.y, payload.center.z);
                    data.Rotation = new Vector3(payload.rotation.x, payload.rotation.y, payload.rotation.z);
                    data.Size = new Vector3(payload.size.x, payload.size.y, payload.size.z);
                    data.Shape = payload.shape ?? "Box";
                }
            }
            catch { }

            return data;
        }

        private static MonsterInfo[] ParseMonsterList(string? json)
        {
            if (string.IsNullOrEmpty(json)) return System.Array.Empty<MonsterInfo>();

            try
            {
                var wrapper = JsonUtility.FromJson<MonsterListJson>(json);
                if (wrapper?.items == null) return System.Array.Empty<MonsterInfo>();

                var result = new MonsterInfo[wrapper.items.Length];
                for (int i = 0; i < wrapper.items.Length; i++)
                {
                    var src = wrapper.items[i];
                    result[i] = new MonsterInfo
                    {
                        monsterId = src.monsterId ?? "",
                        level = src.level,
                        behavior = src.behavior ?? "Idle",
                        guardRadius = src.guardRadius,
                        tag = src.tag ?? "Normal",
                        weight = Mathf.Max(1, src.weight)
                    };
                }
                return result;
            }
            catch
            {
                return System.Array.Empty<MonsterInfo>();
            }
        }

        // ── JSON 反序列化辅助 ──

        [System.Serializable]
        private class AreaPayloadJson
        {
            public Vec3Json center = new();
            public Vec3Json rotation = new();
            public Vec3Json size = new();
            public string shape = "Box";
        }

        [System.Serializable]
        private class Vec3Json
        {
            public float x, y, z;
        }

        [System.Serializable]
        private class MonsterListJson
        {
            public MonsterEntryJson[] items = System.Array.Empty<MonsterEntryJson>();
        }

        [System.Serializable]
        private class MonsterEntryJson
        {
            public string monsterId = "";
            public int level = 1;
            public string behavior = "Idle";
            public float guardRadius = 5f;
            public string tag = "Normal";
            public int weight = 50;
        }

        [System.Serializable]
        private class WaveEntryListJson
        {
            public WaveEntryJson[] items = System.Array.Empty<WaveEntryJson>();
        }

        [System.Serializable]
        private class WaveEntryJson
        {
            public int count = 5;
            public int intervalTicks = 0;
            public string monsterFilter = "All";
        }
    }
}
