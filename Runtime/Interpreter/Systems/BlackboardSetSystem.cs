#nullable enable
using System.Globalization;
using UnityEngine;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 黑板写入系统——处理 Blackboard.Set 节点。
    /// <para>
    /// 运行时逻辑：
    /// 1. 读取 variableName 属性，在 frame.Variables 中查找声明
    /// 2. 根据声明的 Scope 路由：Local → frame.Blackboard，Global → GlobalBlackboard
    /// 3. 根据声明的 Type 解析 value 属性字符串，写入对应 index
    /// 4. 立即完成（ActionPhase.Completed）
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Framework)]
    [UpdateAfter(typeof(FlowSystem))]
    public class BlackboardSetSystem : BlueprintSystemBase
    {
        public override string Name  => "BlackboardSetSystem";

        public override void Update(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Blackboard.Set);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref frame.States[idx];
                if (state.Phase != ActionPhase.Running) continue;
                ProcessSet(frame, idx, ref state);
            }
        }

        private static void ProcessSet(BlueprintFrame frame, int actionIndex, ref ActionRuntimeState state)
        {
            int varIdx   = frame.GetProperty(actionIndex, ActionPortIds.BlackboardSet.VariableIndex, -1);
            string valueStr = frame.GetProperty(actionIndex, ActionPortIds.BlackboardSet.Value, "");

            if (varIdx < 0)
            {
                Debug.LogWarning($"[BlackboardSetSystem] Blackboard.Set (index={actionIndex}) variableIndex 未配置，跳过");
                state.Phase = ActionPhase.Completed;
                return;
            }

            var variable = frame.FindVariable(varIdx);
            if (variable == null)
            {
                Debug.LogWarning($"[BlackboardSetSystem] Blackboard.Set (index={actionIndex}) 未找到 Index={varIdx} 的声明变量");
                state.Phase = ActionPhase.Completed;
                return;
            }

            var parsed = ParseValue(variable.Type, valueStr);

            if (variable.Scope == "Global")
                GlobalBlackboard.Set(variable.Index, parsed);
            else
                frame.Blackboard.Set(variable.Index, parsed);

            Debug.Log($"[BlackboardSetSystem] {variable.Scope}.{variable.Name}[{variable.Index}] ← {parsed}");

            state.Phase = ActionPhase.Completed;
        }

        private static object ParseValue(string type, string value)
        {
            return type?.ToLowerInvariant() switch
            {
                "int"    => int.TryParse(value, out var i) ? i : 0,
                "float"  => float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var f) ? f : 0f,
                "bool"   => bool.TryParse(value, out var b) && b,
                "string" => value ?? "",
                _        => value ?? ""
            };
        }
    }
}
