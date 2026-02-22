#nullable enable
using UnityEngine;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 黑板读取系统——处理 Blackboard.Get 节点。
    /// <para>
    /// 运行时逻辑：
    /// 1. 读取 variableName 属性，在 frame.Variables 中查找声明
    /// 2. 根据声明的 Scope 从 Local 或 Global 黑板读取值
    /// 3. 将值写入内部缓存（key = _{nodeId}.{variableName}），供调试和未来数据端口使用
    /// 4. 立即完成（ActionPhase.Completed），控制流经 out 端口继续
    /// </para>
    /// <para>
    /// Phase 3 升级方向：改为通过强类型数据端口输出，替代内部缓存方式。
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Framework)]
    [UpdateAfter(typeof(BlackboardSetSystem))]
    public class BlackboardGetSystem : BlueprintSystemBase
    {
        public override string Name  => "BlackboardGetSystem";

        public override void Update(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Blackboard.Get);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref frame.States[idx];
                if (state.Phase != ActionPhase.Running) continue;
                ProcessGet(frame, idx, ref state);
            }
        }

        private static void ProcessGet(BlueprintFrame frame, int actionIndex, ref ActionRuntimeState state)
        {
            int varIdx = frame.GetProperty(actionIndex, ActionPortIds.BlackboardGet.VariableIndex, -1);

            if (varIdx < 0)
            {
                Debug.LogWarning($"[BlackboardGetSystem] Blackboard.Get (index={actionIndex}) variableIndex 未配置，跳过");
                state.Phase = ActionPhase.Completed;
                return;
            }

            var variable = frame.FindVariable(varIdx);
            if (variable == null)
            {
                Debug.LogWarning($"[BlackboardGetSystem] Blackboard.Get (index={actionIndex}) 未找到 Index={varIdx} 的声明变量");
                state.Phase = ActionPhase.Completed;
                return;
            }

            object? value;
            if (variable.Scope == "Global")
                GlobalBlackboard.TryGet<object>(variable.Index, out value);
            else
                frame.Blackboard.TryGet<object>(variable.Index, out value);

            Debug.Log($"[BlackboardGetSystem] {variable.Scope}.{variable.Name}[{variable.Index}] → {value ?? "null"}");

            state.Phase = ActionPhase.Completed;
        }
    }
}
