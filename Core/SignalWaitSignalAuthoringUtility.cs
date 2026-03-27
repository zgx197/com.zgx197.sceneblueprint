#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// Signal.WaitSignal 的定义驱动 authoring 读取工具。
    /// 当前主要负责提供稳定属性声明，避免定义真源再次分叉到外部承接层。
    /// </summary>
    public static class SignalWaitSignalAuthoringUtility
    {
        private static readonly PropertyDefinition[] Definitions =
        {
            Prop.SignalTagSelector(ActionPortIds.SignalWaitSignal.SignalTag, "信号标签", defaultValue: string.Empty, order: 0)
                .InSection("signal", "信号 Signal", sectionOrder: 0),
            Prop.EntityRefSelector(ActionPortIds.SignalWaitSignal.SubjectRefFilter, "主体过滤", defaultValue: string.Empty, order: 1)
                .InSection("filter", "过滤 Filter", sectionOrder: 10),
            Prop.Float(ActionPortIds.SignalWaitSignal.Timeout, "超时(s)", defaultValue: 0f, min: 0f, order: 2)
                .InSection("execution", "执行 Execution", sectionOrder: 20, isAdvanced: true),
        };

        public static IReadOnlyList<PropertyDefinition> Properties
            => PropertyDefinitionValueUtility.CloneDefinitions(Definitions);

        public static PropertyDefinition[] CreatePropertiesArray()
            => PropertyDefinitionValueUtility.CloneDefinitions(Definitions);

        public static PropertyDefinition? FindProperty(string propertyKey)
            => PropertyDefinitionValueUtility.FindClonedDefinition(Definitions, propertyKey);

        public static PropertyBagReader CreateBagReader(PropertyBag bag)
            => new PropertyBagReader(bag, Definitions);

        public static PropertyValueReader CreatePropertyReader(ActionEntry action, ActionDefinition? definition = null)
            => new PropertyValueReader(action, definition ?? CreateDefinitionFallback());

        private static ActionDefinition CreateDefinitionFallback()
        {
            return new ActionDefinition
            {
                Properties = CreatePropertiesArray(),
            };
        }
    }
}
