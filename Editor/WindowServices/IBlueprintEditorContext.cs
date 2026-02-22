#nullable enable

namespace SceneBlueprint.Editor.WindowServices
{
    /// <summary>
    /// 蓝图编辑器上下文复合接口：只读状态 + UI 副作用。
    /// 导出相关能力（分析、绑定收集）已直接注入到对应服务，不再通过上下文接口传递。
    /// </summary>
    public interface IBlueprintEditorContext
        : IBlueprintReadContext, IBlueprintUIContext
    {
    }
}
