#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor.Export
{
    /// <summary>
    /// 导出后处理的上下文，打包传递给 <see cref="IExportEnricher"/>。
    /// </summary>
    public sealed class ExportContext
    {
        /// <summary>已完成基础导出的蓝图数据（Actions/Transitions/Bindings 已就绪）</summary>
        public SceneBlueprintData Data { get; }

        /// <summary>ActionRegistry，可用于查询节点定义</summary>
        public ActionRegistry Registry { get; }

        /// <summary>验证消息列表，可追加 Info/Warning/Error</summary>
        public List<ValidationMessage> Messages { get; }

        /// <summary>
        /// 业务侧自定义数据（由调用方通过 ExportOptions.UserData 传入）。
        /// <para>例如通过 <c>context.UserData["levelId"]</c> 获取关卡 ID。</para>
        /// </summary>
        public IReadOnlyDictionary<string, object> UserData { get; }

        public ExportContext(
            SceneBlueprintData data,
            ActionRegistry registry,
            List<ValidationMessage> messages,
            IReadOnlyDictionary<string, object>? userData = null)
        {
            Data = data;
            Registry = registry;
            Messages = messages;
            UserData = userData ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// 当前导出数据上的 transport metadata 运输壳。
        /// 默认读取请优先走这里，而不是直接访问 <see cref="SceneBlueprintData.Metadata"/>。
        /// </summary>
        public PropertyValue[] TransportMetadata => SceneBlueprintTransportMetadataUtility.Read(Data);

        /// <summary>尝试从 UserData 中获取指定类型的值。</summary>
        public bool TryGetUserData<T>(string key, out T value)
        {
            if (UserData.TryGetValue(key, out var obj) && obj is T typed)
            {
                value = typed;
                return true;
            }
            value = default!;
            return false;
        }

        public void ClearTransportMetadata()
        {
            SceneBlueprintTransportMetadataUtility.Clear(Data);
        }

        public void ReplaceTransportMetadata(PropertyValue[]? transportMetadata)
        {
            SceneBlueprintTransportMetadataUtility.Replace(Data, transportMetadata);
        }

        public void AppendTransportMetadata(IEnumerable<PropertyValue>? transportMetadata)
        {
            SceneBlueprintTransportMetadataUtility.Append(Data, transportMetadata);
        }
    }

    /// <summary>
    /// 导出后处理扩展点——在蓝图导出的最终阶段对 <see cref="SceneBlueprintData"/> 进行enrichment。
    /// <para>
    /// 实现此接口并标注 <see cref="ExportEnricherAttribute"/> 后，
    /// <see cref="BlueprintExporter"/> 会在导出流程的最后阶段自动发现并执行。
    /// </para>
    /// <para>
    /// 典型用途：
    /// - 统计蓝图中怪物总数、按类型分布等业务指标
    /// - 注入资源预加载清单
    /// - 计算难度系数等派生数据
    /// </para>
    /// <para>
    /// 所有补充产物应写入顶层 transport metadata 运输壳；
    /// 默认应优先使用 <see cref="ExportContext.TransportMetadata"/>、
    /// <see cref="ExportContext.ReplaceTransportMetadata(PropertyValue[])"/>、
    /// <see cref="ExportContext.AppendTransportMetadata(IEnumerable{PropertyValue})"/>，
    /// 而不是直接把 <see cref="SceneBlueprintData.Metadata"/> 当作长期主协议。
    /// </para>
    /// </summary>
    public interface IExportEnricher
    {
        /// <summary>
        /// 对已组装完成的导出数据进行后处理enrichment。
        /// </summary>
        /// <param name="context">导出上下文（包含蓝图数据、Registry、消息列表、业务自定义数据）</param>
        void Enrich(ExportContext context);
    }

    /// <summary>
    /// 标记一个 <see cref="IExportEnricher"/> 实现类，使其被 <see cref="BlueprintExporter"/> 自动发现。
    /// <para>
    /// 多个 Enricher 按 <see cref="Order"/> 升序执行（默认 0）。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ExportEnricherAttribute : Attribute
    {
        /// <summary>执行顺序（升序，默认 0）。相同 Order 的 Enricher 执行顺序不确定。</summary>
        public int Order { get; set; } = 0;
    }
}
