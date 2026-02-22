#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 蓝图数据加载器——将导出的 JSON / SceneBlueprintData 转换为运行时 BlueprintFrame。
    /// <para>
    /// 职责：
    /// 1. JSON 反序列化 → SceneBlueprintData
    /// 2. 构建索引表（ActionId→Index、TypeId→Indices、出边表）
    /// 3. 初始化 ActionRuntimeState 数组和 Blackboard
    /// </para>
    /// <para>
    /// 对齐 FrameSyncEngine 的数据加载模式：
    /// 类似于 FSGame.CreateFrame()，将静态配置数据灌入 Frame。
    /// </para>
    /// </summary>
    public static class BlueprintLoader
    {
        /// <summary>
        /// 从 JSON 文本加载并构建 BlueprintFrame。
        /// </summary>
        public static BlueprintFrame? Load(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                Debug.LogError("[BlueprintLoader] JSON 文本为空");
                return null;
            }

            Debug.Log($"[BlueprintLoader] 开始解析 JSON，长度: {jsonText.Length} 字符");

            // 注意：JsonUtility 对 ConditionData.Children（递归自引用）会产生
            // "Serialization depth limit 10 exceeded" 警告，不影响数据正确性。
            // Phase 1 不使用 Children 字段；后续迁移到帧同步框架时会使用自定义解析器。
            SceneBlueprintData? data;
            try
            {
                data = JsonUtility.FromJson<SceneBlueprintData>(jsonText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BlueprintLoader] JSON 解析失败: {ex.Message}");
                return null;
            }

            if (data == null)
            {
                Debug.LogError("[BlueprintLoader] JSON 解析结果为 null");
                return null;
            }

            Debug.Log($"[BlueprintLoader] JSON 解析成功: BlueprintId={data.BlueprintId}, " +
                      $"Actions={data.Actions?.Length ?? 0}, Transitions={data.Transitions?.Length ?? 0}");

            // 打印所有 Action 的 TypeId
            if (data.Actions != null && data.Actions.Length > 0)
            {
                Debug.Log($"[BlueprintLoader] Actions 列表:");
                for (int i = 0; i < data.Actions.Length; i++)
                {
                    var action = data.Actions[i];
                    Debug.Log($"  [{i}] TypeId={action.TypeId}, Id={action.Id}");
                }
            }

            return BuildFrame(data);
        }

        /// <summary>
        /// 从已解析的 SceneBlueprintData 构建 BlueprintFrame。
        /// </summary>
        public static BlueprintFrame? BuildFrame(SceneBlueprintData data)
        {
            Debug.Log($"[BlueprintLoader.BuildFrame] 开始构建 Frame，Actions 数量: {data.Actions?.Length ?? 0}");

            if (data.Actions == null || data.Actions.Length == 0)
            {
                Debug.LogWarning("[BlueprintLoader] 蓝图中无 Action 数据");
                return null;
            }

            var frame = new BlueprintFrame
            {
                BlueprintId = data.BlueprintId ?? "",
                BlueprintName = data.BlueprintName ?? "",
                Actions = data.Actions,
                Transitions = data.Transitions ?? Array.Empty<TransitionEntry>(),
            };

            Debug.Log($"[BlueprintLoader.BuildFrame] Frame 创建完成，Actions={frame.Actions.Length}, Transitions={frame.Transitions.Length}");

            // ── 1. 构建 ActionId → Index 映射 ──
            var idToIndex = new Dictionary<string, int>(data.Actions.Length);
            for (int i = 0; i < data.Actions.Length; i++)
            {
                var id = data.Actions[i].Id;
                if (!string.IsNullOrEmpty(id))
                {
                    idToIndex[id] = i;
                }
            }
            frame.ActionIdToIndex = idToIndex;
            Debug.Log($"[BlueprintLoader.BuildFrame] ActionId 映射表构建完成，共 {idToIndex.Count} 个映射");

            // ── 2. 构建 TypeId → ActionIndex 列表 ──
            var byType = new Dictionary<string, List<int>>();
            int startIndex = -1;
            for (int i = 0; i < data.Actions.Length; i++)
            {
                var typeId = data.Actions[i].TypeId;
                if (!byType.TryGetValue(typeId, out var list))
                {
                    list = new List<int>();
                    byType[typeId] = list;
                }
                list.Add(i);

                // 记录 Flow.Start 节点索引
                if (typeId == "Flow.Start")
                {
                    startIndex = i;
                }
            }
            frame.ActionsByTypeId = byType;
            frame.StartActionIndex = startIndex;

            Debug.Log($"[BlueprintLoader.BuildFrame] TypeId 索引构建完成:");
            foreach (var kvp in byType)
            {
                Debug.Log($"  {kvp.Key}: {kvp.Value.Count} 个节点 (indices: {string.Join(", ", kvp.Value)})");
            }
            Debug.Log($"[BlueprintLoader.BuildFrame] Flow.Start 索引: {startIndex}");

            // ── 3. 构建出边索引（ActionIndex → 出发的 Transition 索引列表）──
            var outgoing = new Dictionary<int, List<int>>();
            for (int i = 0; i < frame.Transitions.Length; i++)
            {
                var t = frame.Transitions[i];
                if (idToIndex.TryGetValue(t.FromActionId, out var fromIdx))
                {
                    if (!outgoing.TryGetValue(fromIdx, out var list))
                    {
                        list = new List<int>();
                        outgoing[fromIdx] = list;
                    }
                    list.Add(i);
                }
            }
            frame.OutgoingTransitions = outgoing;
            Debug.Log($"[BlueprintLoader.BuildFrame] 出边索引构建完成，共 {outgoing.Count} 个节点有出边");

            // ── 4. 初始化运行时状态数组（全部 Idle）──
            var states = new ActionRuntimeState[data.Actions.Length];
            for (int i = 0; i < states.Length; i++)
            {
                states[i].Reset();
            }
            frame.States = states;
            Debug.Log($"[BlueprintLoader.BuildFrame] 运行时状态数组初始化完成，长度: {states.Length}");

            // ── 5. 初始化 Blackboard ──
            var bb = new Blackboard();
            var variables = data.Variables ?? Array.Empty<VariableDeclaration>();
            foreach (var decl in variables)
            {
                if (decl.Index < 0) continue;
                var parsed = ParseVariableValue(decl.Type, decl.InitialValue);
                if (decl.Scope == "Global")
                    GlobalBlackboard.SetIfAbsent(decl.Index, parsed);
                else
                    bb.Set(decl.Index, parsed);
            }
            frame.Blackboard = bb;
            frame.Variables  = variables;
            Debug.Log($"[BlueprintLoader.BuildFrame] Blackboard 初始化完成，Local={bb.DeclaredCount} Global={GlobalBlackboard.DeclaredCount}");

            // ── 6. 构建 DataInConnections 反向索引 ──
            var dataInIndex = new Dictionary<(int, string), (int, string)>();
            var dataConns = data.DataConnections ?? Array.Empty<DataConnectionEntry>();
            foreach (var dc in dataConns)
            {
                if (!idToIndex.TryGetValue(dc.FromActionId, out int fromIdx)) continue;
                if (!idToIndex.TryGetValue(dc.ToActionId,   out int toIdx))   continue;
                dataInIndex[(toIdx, dc.ToPortId)] = (fromIdx, dc.FromPortId);
            }
            frame.DataInConnections = dataInIndex;
            Debug.Log($"[BlueprintLoader.BuildFrame] DataInConnections 索引构建完成，共 {dataInIndex.Count} 条数据连接");

            // ── 7. 构建 DataPortDefaults 索引 ──
            int defaultsCount = 0;
            foreach (var action in data.Actions)
            {
                if (!idToIndex.TryGetValue(action.Id, out int idx)) continue;
                if (action.PortDefaults == null || action.PortDefaults.Length == 0) continue;
                foreach (var pd in action.PortDefaults)
                {
                    if (!string.IsNullOrEmpty(pd.PortId))
                    {
                        frame.DataPortDefaults[(idx, pd.PortId)] = pd.DefaultValue ?? "";
                        defaultsCount++;
                    }
                }
            }
            Debug.Log($"[BlueprintLoader.BuildFrame] DataPortDefaults 构建完成，共 {defaultsCount} 个端口默认值");

            Debug.Log($"[BlueprintLoader.BuildFrame] ✓ Frame 构建完成，最终 ActionCount={frame.ActionCount}");

            return frame;
        }

        /// <summary>
        /// 简单的变量值解析（字符串 → 对应类型）。
        /// Phase 1 仅支持基础类型，后续按需扩展。
        /// </summary>
        private static object ParseVariableValue(string valueType, string value)
        {
            switch (valueType?.ToLowerInvariant())
            {
                case "int":
                    return int.TryParse(value, out var i) ? i : 0;
                case "float":
                    return float.TryParse(value, out var f) ? f : 0f;
                case "bool":
                    return bool.TryParse(value, out var b) && b;
                case "string":
                    return value ?? "";
                default:
                    return value ?? "";
            }
        }
    }
}
