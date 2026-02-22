#nullable enable
using System;
using NodeGraph.Core;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 连线验证结果
    /// </summary>
    public class ValidationResult
    {
        /// <summary>是否验证通过</summary>
        public bool IsValid { get; }
        
        /// <summary>错误或警告信息（验证失败时）</summary>
        public string Message { get; }
        
        /// <summary>严重程度</summary>
        public ValidationSeverity Severity { get; }

        private ValidationResult(bool isValid, string message, ValidationSeverity severity)
        {
            IsValid = isValid;
            Message = message;
            Severity = severity;
        }

        /// <summary>创建成功的验证结果</summary>
        public static ValidationResult Success() 
            => new ValidationResult(true, "", ValidationSeverity.None);

        /// <summary>创建错误的验证结果</summary>
        public static ValidationResult Error(string message) 
            => new ValidationResult(false, message, ValidationSeverity.Error);

        /// <summary>创建警告的验证结果</summary>
        public static ValidationResult Warning(string message) 
            => new ValidationResult(true, message, ValidationSeverity.Warning);
    }

    /// <summary>验证严重程度</summary>
    public enum ValidationSeverity
    {
        None,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 连线验证器——验证两个端口是否可以连接。
    /// <para>
    /// 验证规则：
    /// 1. 同类型端口才能连接（Flow→Flow, Event→Event, Data→Data）
    /// 2. 方向必须匹配（Output → Input）
    /// 3. Data 端口必须类型兼容
    /// 4. Flow 输入端口只能连一条线（Single 容量检查）
    /// 5. Data 输入端口只能连一条线
    /// </para>
    /// </summary>
    public class ConnectionValidator
    {
        /// <summary>
        /// 验证两个端口是否可以连接
        /// </summary>
        /// <param name="sourcePort">源端口（必须是 Output）</param>
        /// <param name="targetPort">目标端口（必须是 Input）</param>
        /// <returns>验证结果</returns>
        public ValidationResult ValidateConnection(
            SceneBlueprint.Core.PortDefinition sourcePort, 
            SceneBlueprint.Core.PortDefinition targetPort)
        {
            if (sourcePort == null || targetPort == null)
                return ValidationResult.Error("端口不能为空");

            // R1: 同类型端口才能连接
            if (sourcePort.Kind != targetPort.Kind)
            {
                return ValidationResult.Error(
                    $"端口类型不匹配：{GetKindName(sourcePort.Kind)} 无法连接到 {GetKindName(targetPort.Kind)}");
            }

            // R2: 方向必须匹配（Output → Input）
            if (sourcePort.Direction != NodeGraph.Core.PortDirection.Output)
            {
                return ValidationResult.Error("源端口必须是输出端口");
            }

            if (targetPort.Direction != NodeGraph.Core.PortDirection.Input)
            {
                return ValidationResult.Error("目标端口必须是输入端口");
            }

            // R3: Data 端口类型兼容性检查
            if (sourcePort.Kind == NodeGraph.Core.PortKind.Data)
            {
                if (!IsTypeCompatible(sourcePort.DataType, targetPort.DataType))
                {
                    return ValidationResult.Error(
                        $"数据类型不兼容\n源端口: {sourcePort.DataType}\n目标端口: {targetPort.DataType}");
                }
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// 检查两个数据类型是否兼容（使用 DataTypeRegistry 支持子类型检查）
        /// </summary>
        /// <param name="sourceType">源类型</param>
        /// <param name="targetType">目标类型</param>
        /// <returns>是否兼容</returns>
        public bool IsTypeCompatible(string sourceType, string targetType)
        {
            // 使用 DataTypeRegistry 进行兼容性检查（支持子类型）
            return DataTypeRegistry.Instance.IsCompatible(sourceType, targetType);
        }

        /// <summary>
        /// 获取端口类型的显示名称
        /// </summary>
        private string GetKindName(NodeGraph.Core.PortKind kind)
        {
            return kind switch
            {
                NodeGraph.Core.PortKind.Control => "控制流",
                NodeGraph.Core.PortKind.Event => "事件流",
                NodeGraph.Core.PortKind.Data => "数据流",
                _ => kind.ToString()
            };
        }

        /// <summary>
        /// 验证端口容量限制（是否已达到连接上限）
        /// </summary>
        /// <param name="port">要检查的端口</param>
        /// <param name="currentConnectionCount">当前已连接的数量</param>
        /// <returns>验证结果</returns>
        public ValidationResult ValidateCapacity(SceneBlueprint.Core.PortDefinition port, int currentConnectionCount)
        {
            if (port.Capacity == NodeGraph.Core.PortCapacity.Single && currentConnectionCount >= 1)
            {
                return ValidationResult.Error(
                    $"端口 '{port.DisplayName}' 只能连接一条线");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// 检查必需的 Data 输入端口是否已连接
        /// </summary>
        /// <param name="port">要检查的端口</param>
        /// <param name="isConnected">是否已连接</param>
        /// <returns>验证结果</returns>
        public ValidationResult ValidateRequired(SceneBlueprint.Core.PortDefinition port, bool isConnected)
        {
            if (port.Kind == NodeGraph.Core.PortKind.Data 
                && port.Direction == NodeGraph.Core.PortDirection.Input 
                && port.Required 
                && !isConnected)
            {
                return ValidationResult.Warning(
                    $"必需的数据端口 '{port.DisplayName}' 未连接");
            }

            return ValidationResult.Success();
        }
    }
}
