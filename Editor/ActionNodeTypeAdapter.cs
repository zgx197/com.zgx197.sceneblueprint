#nullable enable
using System.Collections.Generic;
using NodeGraph.Core;
using NodeGraph.Math;
using SceneBlueprint.Core;

// 使用别名消除两个框架中同名类型的歧义
using NGPortDef = NodeGraph.Core.PortDefinition;
using SBPortDef = SceneBlueprint.Core.PortDefinition;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// ActionDefinition → NodeTypeDefinition 适配器。
    /// 将 SceneBlueprint 的行动定义桥接到 NodeGraph 的节点类型系统。
    /// </summary>
    public static class ActionNodeTypeAdapter
    {
        /// <summary>
        /// 将单个 ActionDefinition 转换为 NodeTypeDefinition。
        /// </summary>
        public static NodeTypeDefinition Convert(ActionDefinition actionDef)
        {
            // 转换端口定义
            var ngPorts = ConvertPorts(actionDef.Ports);

            var nodeTypeDef = new NodeTypeDefinition(
                typeId: actionDef.TypeId,
                displayName: actionDef.DisplayName,
                category: actionDef.Category,
                defaultPorts: ngPorts
            )
            {
                Color = actionDef.ThemeColor,
                // 创建默认业务数据：从 ActionDefinition 初始化 ActionNodeData（含默认属性值）
                CreateDefaultData = () => ActionNodeData.CreateFromDefinition(actionDef)
            };

            return nodeTypeDef;
        }

        /// <summary>
        /// 将 SceneBlueprint 端口定义数组转换为 NodeGraph 端口定义数组。
        /// </summary>
        private static NGPortDef[] ConvertPorts(SBPortDef[] sbPorts)
        {
            if (sbPorts == null || sbPorts.Length == 0)
                return System.Array.Empty<NGPortDef>();

            var result = new NGPortDef[sbPorts.Length];
            for (int i = 0; i < sbPorts.Length; i++)
            {
                result[i] = ConvertPort(sbPorts[i], i);
            }
            return result;
        }

        /// <summary>
        /// 将单个 SceneBlueprint 端口定义转换为 NodeGraph 端口定义。
        /// </summary>
        private static NGPortDef ConvertPort(SBPortDef sbPort, int sortOrder)
        {
            var direction = sbPort.Direction == PortDirection.Input
                ? PortDirection.Input
                : PortDirection.Output;

            var capacity = sbPort.Capacity == PortCapacity.Single
                ? PortCapacity.Single
                : PortCapacity.Multiple;

            // 正确传递端口的 Kind 和 DataType
            // SceneBlueprint.Core.PortDefinition.Kind 和 NodeGraph.Core.PortKind 使用相同的枚举值
            var kind = sbPort.Kind;
            // Data 端口空 DataType = DataTypes.Any，保持空串；Control/Event 端口用 "exec" 占位
            var dataType = (kind == PortKind.Data)
                ? sbPort.DataType
                : (string.IsNullOrEmpty(sbPort.DataType) ? "exec" : sbPort.DataType);

            return new NGPortDef(
                name: string.IsNullOrEmpty(sbPort.DisplayName) ? sbPort.Id : sbPort.DisplayName,
                direction: direction,
                kind: kind,
                dataType: dataType,
                capacity: capacity,
                sortOrder: sortOrder,
                semanticId: sbPort.Id
            );
        }
    }
}
