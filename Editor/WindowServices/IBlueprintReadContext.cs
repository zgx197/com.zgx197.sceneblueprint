#nullable enable
using NodeGraph.View;
using SceneBlueprint.Core;
using SceneBlueprint.Runtime;
using UnityEngine;

namespace SceneBlueprint.Editor.WindowServices
{
    /// <summary>
    /// 只读上下文接口——服务类对图状态和工具注册表的被动访问。
    /// 不包含任何写操作或副作用触发。
    /// </summary>
    public interface IBlueprintReadContext
    {
        /// <summary>当前图 ViewModel（服务类需自行判空）</summary>
        GraphViewModel? ViewModel { get; }

        /// <summary>当前蓝图资产（服务类需自行判空）</summary>
        BlueprintAsset? CurrentAsset { get; }

        /// <summary>ActionRegistry（始终非 null）</summary>
        ActionRegistry ActionRegistry { get; }

        /// <summary>返回编辑器窗口当前宽高</summary>
        Vector2 GetWindowSize();

        /// <summary>获取当前空间适配器类型标识</summary>
        string GetAdapterType();
    }
}
