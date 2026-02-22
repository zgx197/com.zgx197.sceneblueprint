#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Templates
{
    /// <summary>
    /// 策划通过 Inspector 配置的 Action 类型模板（ScriptableObject）。
    /// <para>
    /// 与 C# <c>IActionDefinitionProvider</c> 等价，但无需编写代码。
    /// 策划在 Inspector 中填写属性、端口、场景需求等信息，
    /// 编辑器启动时由 <c>ActionTemplateConverter</c> 转换为 <c>ActionDefinition</c> 并注册。
    /// </para>
    /// <para>
    /// 当 TypeId 与 C# 定义冲突时，C# 定义优先，SO 模板被跳过并输出警告。
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewActionTemplate",
        menuName = "SceneBlueprint/Action Template",
        order = 100)]
    public class ActionTemplateSO : ScriptableObject
    {
        // ═══════════════════════════════════════════════════════════
        //  元数据
        // ═══════════════════════════════════════════════════════════

        [Header("── 元数据 ──")]
        [Tooltip("全局唯一类型 ID，格式为 'Category.Name'，如 'Cinematic.PlayTimeline'")]
        public string TypeId = "";

        [Tooltip("编辑器中显示的名称，如 '播放时间线'")]
        public string DisplayName = "";

        [Tooltip("分类，用于右键菜单和搜索窗分组，如 'Cinematic'")]
        public string Category = "";

        [Tooltip("节点主题色")]
        public Color ThemeColor = Color.gray;

        [Tooltip("图标标识（可选）")]
        public string Icon = "";

        [TextArea(2, 4)]
        [Tooltip("描述文本，悬停节点时显示")]
        public string Description = "";

        [Tooltip("行动时长类型")]
        public ActionDurationEntry Duration = ActionDurationEntry.Instant;

        // ═══════════════════════════════════════════════════════════
        //  端口
        // ═══════════════════════════════════════════════════════════

        [Header("── 输出端口 ──")]
        [Tooltip("输出端口列表（输入端口默认有一个'激活'，无需配置）")]
        public List<PortEntry> OutputPorts = new()
        {
            new PortEntry { Id = "out", DisplayName = "完成", PortType = PortTypeEntry.FlowOut }
        };

        // ═══════════════════════════════════════════════════════════
        //  属性
        // ═══════════════════════════════════════════════════════════

        [Header("── 属性 ──")]
        public List<PropertyEntry> Properties = new();

        // ═══════════════════════════════════════════════════════════
        //  场景需求
        // ═══════════════════════════════════════════════════════════

        [Header("── 场景需求 ──")]
        [Tooltip("声明该 Action 需要什么类型的场景标记")]
        public List<SceneRequirementEntry> SceneRequirements = new();

        // ═══════════════════════════════════════════════════════════
        //  嵌套序列化类型
        // ═══════════════════════════════════════════════════════════

        /// <summary>行动时长类型（对应 Core.ActionDuration 枚举）</summary>
        public enum ActionDurationEntry
        {
            /// <summary>瞬时行动——执行一次就完成</summary>
            Instant,
            /// <summary>持续行动——有运行状态，需要等待完成</summary>
            Duration,
            /// <summary>被动行动——条件满足时自动响应</summary>
            Passive
        }

        /// <summary>端口类型</summary>
        public enum PortTypeEntry
        {
            /// <summary>流程输出（单连接）</summary>
            FlowOut,
            /// <summary>事件输出（多连接）</summary>
            EventOut
        }

        /// <summary>端口条目</summary>
        [Serializable]
        public class PortEntry
        {
            [Tooltip("端口 ID，同一节点内唯一")]
            public string Id = "";

            [Tooltip("显示名称")]
            public string DisplayName = "";

            [Tooltip("端口类型：FlowOut(单连接) / EventOut(多连接)")]
            public PortTypeEntry PortType = PortTypeEntry.FlowOut;
        }

        /// <summary>属性值类型（对应 Core.PropertyType 枚举）</summary>
        public enum PropertyTypeEntry
        {
            Float, Int, Bool, String, Enum, AssetRef,
            Vector2, Vector3, Color, Tag, SceneBinding
        }

        /// <summary>场景绑定类型（对应 Core.BindingType 枚举）</summary>
        public enum BindingTypeEntry
        {
            Transform, Area, Path, Collider
        }

        /// <summary>属性条目</summary>
        [Serializable]
        public class PropertyEntry
        {
            [Tooltip("属性键名，同一节点内唯一")]
            public string Key = "";

            [Tooltip("显示名称")]
            public string DisplayName = "";

            [Tooltip("属性值类型")]
            public PropertyTypeEntry Type = PropertyTypeEntry.String;

            [Tooltip("默认值（统一用字符串表示，运行时按类型解析）")]
            public string DefaultValue = "";

            [Header("── 数值约束（Float/Int） ──")]
            [Tooltip("最小值")]
            public float Min;

            [Tooltip("最大值")]
            public float Max = 100f;

            [Tooltip("是否启用范围约束")]
            public bool UseRange;

            [Header("── 枚举选项（Enum） ──")]
            [Tooltip("枚举选项，逗号分隔，如 'Instant,Interval,Burst'")]
            public string EnumOptions = "";

            [Header("── 场景绑定（SceneBinding） ──")]
            [Tooltip("场景绑定类型")]
            public BindingTypeEntry BindingType = BindingTypeEntry.Transform;

            [Header("── 资产引用（AssetRef） ──")]
            [Tooltip("资产类型过滤（完整类名，如 'MonsterGroupTemplate'）")]
            public string AssetFilterTypeName = "";

            [Header("── UI 控制 ──")]
            [Tooltip("条件可见性表达式，如 'tempoType == Interval'")]
            public string VisibleWhen = "";

            [Tooltip("Inspector 分组名")]
            public string Category = "";

            [Tooltip("排列顺序（小值在前）")]
            public int Order;

            [Tooltip("悬停提示")]
            public string Tooltip = "";
        }

        /// <summary>场景需求条目</summary>
        [Serializable]
        public class SceneRequirementEntry
        {
            [Tooltip("绑定键名（如 'spawnArea'）")]
            public string BindingKey = "";

            [Tooltip("标记类型 ID（如 'Point', 'Area'）")]
            public string MarkerTypeId = "Point";

            [Tooltip("是否必需")]
            public bool Required = true;

            [Tooltip("是否允许绑定多个标记")]
            public bool AllowMultiple;

            [Tooltip("最少数量（AllowMultiple 时有效）")]
            public int MinCount;

            [Tooltip("显示名称")]
            public string DisplayName = "";

            [Tooltip("创建标记时的默认 Tag")]
            public string DefaultTag = "";
        }
    }
}
