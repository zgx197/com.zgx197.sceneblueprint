#nullable enable
using System.Linq;
using NodeGraph.Abstraction;
using NodeGraph.Core;
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 通用行动节点内容渲染器。
    /// 根据 ActionDefinition 的 PropertyDefinition[] 自动生成 Inspector 控件。
    /// 所有 Action 类型共享此渲染器，无需为每种 Action 手写 Renderer。
    /// </summary>
    public class ActionContentRenderer : INodeContentRenderer
    {
        private readonly ActionRegistry _actionRegistry;

        /// <summary>每行属性的高度（像素）</summary>
        private const float LineHeight = 18f;
        /// <summary>行间距</summary>
        private const float LineSpacing = 2f;
        /// <summary>内容区最小宽度</summary>
        private const float MinContentWidth = 140f;
        /// <summary>摘要显示的最大属性数</summary>
        private const int MaxSummaryLines = 4;

        public ActionContentRenderer(ActionRegistry actionRegistry)
        {
            _actionRegistry = actionRegistry;
        }

        public bool SupportsInlineEdit => false;

        // ── 摘要视图 ──

        public Vec2 GetSummarySize(Node node, ITextMeasurer measurer)
        {
            var data = node.UserData as ActionNodeData;
            if (data == null) return new Vec2(MinContentWidth, LineHeight);

            if (!_actionRegistry.TryGet(data.ActionTypeId, out var def))
                return new Vec2(MinContentWidth, LineHeight);

            int lineCount = System.Math.Min(def.Properties.Length, MaxSummaryLines);
            if (lineCount == 0) lineCount = 1;
            float height = lineCount * (LineHeight + LineSpacing);
            return new Vec2(MinContentWidth, height);
        }

        public NodeContentInfo GetSummaryInfo(Node node, Rect2 contentRect)
        {
            var info = new NodeContentInfo
            {
                ContentRect = contentRect,
                Node = node,
                TypeId = "ActionSummary"
            };

            var data = node.UserData as ActionNodeData;
            if (data == null)
            {
                info.SummaryLines.Add("(无数据)");
                return info;
            }

            if (!_actionRegistry.TryGet(data.ActionTypeId, out var def))
            {
                info.SummaryLines.Add($"(未知类型: {data.ActionTypeId})");
                return info;
            }

            // 显示关键属性值作为摘要
            int count = 0;
            foreach (var prop in def.Properties)
            {
                if (count >= MaxSummaryLines) break;

                // 检查 VisibleWhen 条件
                if (!string.IsNullOrEmpty(prop.VisibleWhen))
                {
                    if (!VisibleWhenEvaluator.Evaluate(prop.VisibleWhen, data.Properties))
                        continue;
                }

                var value = data.Properties.GetRaw(prop.Key);
                string displayValue = FormatPropertyValue(prop, value);
                info.SummaryLines.Add($"{prop.DisplayName}: {displayValue}");
                count++;
            }

            if (info.SummaryLines.Count == 0)
                info.SummaryLines.Add($"({def.DisplayName})");

            return info;
        }

        public string GetOneLiner(Node node)
        {
            var data = node.UserData as ActionNodeData;
            if (data == null) return "(无数据)";

            if (!_actionRegistry.TryGet(data.ActionTypeId, out var def))
                return data.ActionTypeId;

            // 取第一个属性作为一行摘要
            if (def.Properties.Length > 0)
            {
                var firstProp = def.Properties[0];
                var value = data.Properties.GetRaw(firstProp.Key);
                return $"{def.DisplayName} | {firstProp.DisplayName}={FormatPropertyValue(firstProp, value)}";
            }

            return def.DisplayName;
        }

        // ── 编辑视图 ──

        public Vec2 GetEditorSize(Node node, IEditContext ctx)
        {
            var data = node.UserData as ActionNodeData;
            if (data == null) return new Vec2(MinContentWidth, LineHeight);

            if (!_actionRegistry.TryGet(data.ActionTypeId, out var def))
                return new Vec2(MinContentWidth, LineHeight);

            // 计算可见属性数量
            int visibleCount = 0;
            foreach (var prop in def.Properties)
            {
                if (!string.IsNullOrEmpty(prop.VisibleWhen))
                {
                    if (!VisibleWhenEvaluator.Evaluate(prop.VisibleWhen, data.Properties))
                        continue;
                }
                visibleCount++;
            }

            if (visibleCount == 0) visibleCount = 1;
            float height = visibleCount * (LineHeight + LineSpacing) + 4f;
            return new Vec2(180f, height);
        }

        public void DrawEditor(Node node, Rect2 rect, IEditContext ctx)
        {
            var data = node.UserData as ActionNodeData;
            if (data == null)
            {
                ctx.Label("(无数据)");
                return;
            }

            if (!_actionRegistry.TryGet(data.ActionTypeId, out var def))
            {
                ctx.Label($"(未知类型: {data.ActionTypeId})");
                return;
            }

            // 按 PropertyDefinition 顺序逐个绘制编辑控件
            foreach (var prop in def.Properties.OrderBy(p => p.Order))
            {
                // 检查条件可见性
                if (!string.IsNullOrEmpty(prop.VisibleWhen))
                {
                    if (!VisibleWhenEvaluator.Evaluate(prop.VisibleWhen, data.Properties))
                        continue;
                }

                DrawPropertyField(prop, data.Properties, ctx);
            }

            // 如果值被修改，通过 HasChanged 标记通知框架
            // （IEditContext 会自动追踪变更）
        }

        // ── 属性控件绘制 ──

        private void DrawPropertyField(PropertyDefinition prop, PropertyBag bag, IEditContext ctx)
        {
            switch (prop.Type)
            {
                case PropertyType.Float:
                {
                    float current = bag.Get<float>(prop.Key);
                    float result;
                    if (prop.Min.HasValue && prop.Max.HasValue)
                        result = ctx.Slider(prop.DisplayName, current, prop.Min.Value, prop.Max.Value);
                    else
                        result = ctx.FloatField(prop.DisplayName, current);
                    if (!result.Equals(current))
                        bag.Set(prop.Key, result);
                    break;
                }

                case PropertyType.Int:
                {
                    int current = bag.Get<int>(prop.Key);
                    int result = ctx.IntField(prop.DisplayName, current);
                    if (result != current)
                        bag.Set(prop.Key, result);
                    break;
                }

                case PropertyType.Bool:
                {
                    bool current = bag.Get<bool>(prop.Key);
                    bool result = ctx.Toggle(prop.DisplayName, current);
                    if (result != current)
                        bag.Set(prop.Key, result);
                    break;
                }

                case PropertyType.String:
                {
                    string current = bag.Get<string>(prop.Key) ?? "";
                    string result = ctx.TextField(prop.DisplayName, current);
                    if (result != current)
                        bag.Set(prop.Key, result);
                    break;
                }

                case PropertyType.Enum:
                {
                    if (prop.EnumOptions != null && prop.EnumOptions.Length > 0)
                    {
                        string current = bag.Get<string>(prop.Key) ?? prop.EnumOptions[0];
                        int selectedIndex = System.Array.IndexOf(prop.EnumOptions, current);
                        if (selectedIndex < 0) selectedIndex = 0;
                        int newIndex = ctx.Popup(prop.DisplayName, selectedIndex, prop.EnumOptions);
                        if (newIndex != selectedIndex && newIndex >= 0 && newIndex < prop.EnumOptions.Length)
                            bag.Set(prop.Key, prop.EnumOptions[newIndex]);
                    }
                    else
                    {
                        ctx.Label($"{prop.DisplayName}: (无枚举选项)");
                    }
                    break;
                }

                case PropertyType.AssetRef:
                {
                    // Phase 2A: 用文本字段显示资产引用路径
                    string current = bag.Get<string>(prop.Key) ?? "";
                    string result = ctx.TextField(prop.DisplayName, current);
                    if (result != current)
                        bag.Set(prop.Key, result);
                    break;
                }

                case PropertyType.SceneBinding:
                {
                    // Phase 2A: 用文本字段显示场景绑定名
                    string current = bag.Get<string>(prop.Key) ?? "";
                    string result = ctx.TextField(prop.DisplayName, current);
                    if (result != current)
                        bag.Set(prop.Key, result);
                    break;
                }

                case PropertyType.Tag:
                {
                    string current = bag.Get<string>(prop.Key) ?? "";
                    string result = ctx.TextField(prop.DisplayName, current);
                    if (result != current)
                        bag.Set(prop.Key, result);
                    break;
                }

                case PropertyType.StructList:
                {
                    // 节点画布中只显示摘要文本，详细编辑在侧边 Inspector
                    var value = bag.GetRaw(prop.Key);
                    string summary = FormatStructListSummary(prop, value);
                    ctx.Label($"{prop.DisplayName}: {summary}");
                    break;
                }

                default:
                {
                    ctx.Label($"{prop.DisplayName}: (不支持的类型 {prop.Type})");
                    break;
                }
            }
        }

        // ── 格式化辅助 ──

        private static string FormatPropertyValue(PropertyDefinition prop, object? value)
        {
            if (value == null) return "(未设置)";

            return prop.Type switch
            {
                PropertyType.Float => $"{value:F1}",
                PropertyType.Int => value.ToString() ?? "0",
                PropertyType.Bool => (bool)value ? "是" : "否",
                PropertyType.String => value.ToString() ?? "",
                PropertyType.Enum => value.ToString() ?? "",
                PropertyType.AssetRef => value.ToString() ?? "(无)",
                PropertyType.SceneBinding => FormatSceneBindingSummary(value),
                PropertyType.Tag => value.ToString() ?? "(无)",
                PropertyType.StructList => FormatStructListSummary(prop, value),
                _ => value.ToString() ?? ""
            };
        }

        /// <summary>
        /// 格式化 SceneBinding 的摘要显示。
        /// PropertyBag 中存储的是 MarkerId（GUID 字符串），截短显示以保持可读性。
        /// </summary>
        private static string FormatSceneBindingSummary(object value)
        {
            var str = value.ToString() ?? "";
            if (string.IsNullOrEmpty(str)) return "(无)";
            // MarkerId 通常是 GUID 格式，截取前 8 位作为摘要
            return str.Length > 8 ? $"[{str[..8]}…]" : str;
        }

        /// <summary>
        /// 格式化 StructList 的摘要显示。
        /// 使用 SummaryFormat 模板，{count} 替换为列表元素数量。
        /// 如果没有 SummaryFormat，显示 "N 项"。
        /// </summary>
        private static string FormatStructListSummary(PropertyDefinition prop, object value)
        {
            var json = value?.ToString() ?? "[]";
            int count = StructListJsonHelper.GetItemCount(json);

            if (!string.IsNullOrEmpty(prop.SummaryFormat))
            {
                return prop.SummaryFormat!.Replace("{count}", count.ToString());
            }

            return count == 0 ? "(空)" : $"{count} 项";
        }
    }
}
