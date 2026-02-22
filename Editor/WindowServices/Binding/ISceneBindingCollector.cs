#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Editor.Export;

namespace SceneBlueprint.Editor.WindowServices.Binding
{
    /// <summary>
    /// 场景绑定收集器接口——供 <see cref="BlueprintExportService"/> 直接注入，
    /// 消除通过 IBlueprintExportContext 委托转发的间接层。
    /// </summary>
    public interface ISceneBindingCollector
    {
        /// <summary>收集导出用的场景绑定数据列表，无绑定时返回 null。</summary>
        List<BlueprintExporter.SceneBindingData>? CollectForExport();
    }
}
