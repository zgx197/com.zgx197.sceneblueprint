#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// Signal.Emit 的定义驱动 authoring 读取工具。
    /// 当前主要负责提供稳定属性声明，避免定义真源再次分叉到外部承接层。
    /// </summary>
    public static class SignalEmitAuthoringUtility
    {
        private static readonly PropertyDefinition[] Definitions =
        {
            Prop.SignalTagSelector(ActionPortIds.SignalEmit.SignalTag, "信号标签", defaultValue: string.Empty, order: 0)
                .InSection("signal", "信号 Signal", sectionOrder: 0),
            Prop.EntityRefSelector(ActionPortIds.SignalEmit.SubjectRef, "主体引用", defaultValue: string.Empty, order: 1)
                .InSection("context", "上下文 Context", sectionOrder: 10),
            Prop.EntityRefSelector(ActionPortIds.SignalEmit.InstigatorRef, "发起者引用", defaultValue: string.Empty, order: 2)
                .InSection("context", "上下文 Context", sectionOrder: 10, isAdvanced: true),
            Prop.EntityRefSelector(ActionPortIds.SignalEmit.TargetRef, "目标引用", defaultValue: string.Empty, order: 3)
                .InSection("context", "上下文 Context", sectionOrder: 10, isAdvanced: true),
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
