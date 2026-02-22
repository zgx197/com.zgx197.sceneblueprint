#nullable enable
using NodeGraph.Core;
using SceneBlueprint.Core;
using GraphPort = NodeGraph.Core.Port;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// SceneBlueprint 专用的 Data 端口类型兼容性验证器（责任链节点）。
    /// <para>
    /// 使用 <see cref="DataTypeRegistry"/> 进行验证，支持子类型层级和数组元素类型比较，
    /// 比 NodeGraph 内置的 <see cref="NodeGraph.Core.TypeCompatibilityRegistry"/> 更丰富。
    /// 仅对 <see cref="PortKind.Data"/> 端口生效，其他 Kind 直接放行。
    /// </para>
    /// </summary>
    public sealed class DataTypeRegistryValidator : IConnectionValidator
    {
        public ConnectionResult? Validate(Graph graph, GraphPort outPort, GraphPort inPort)
        {
            if (outPort.Kind != PortKind.Data)
                return null;

            if (!DataTypeRegistry.Instance.IsCompatible(outPort.DataType, inPort.DataType))
                return ConnectionResult.DataTypeMismatch;

            return null;
        }
    }
}
