#nullable enable

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 为业务层保留“带结构化事件上下文的注入”扩展接缝。
    /// 普通桥接器至少支持无上下文 Send；支持扩展接口时可把上下文一并传给 runtime/debug 域。
    /// </summary>
    public static class SignalBridgeEventContextExtensions
    {
        public static void Send(
            this ISignalBridge bridge,
            SignalTag tag,
            SignalPayload? payload,
            BlueprintEventContext? eventContext)
        {
            if (bridge is IBlueprintEventContextSignalBridge contextAwareBridge)
            {
                contextAwareBridge.Send(tag, payload, eventContext);
                return;
            }

            bridge.Send(tag, payload);
        }
    }
}
