#nullable enable
using System;
using System.Globalization;

namespace SceneBlueprint.Core
{
    // ═══════════════════════════════════════════════════════════
    //  VisibleWhen 条件评估器
    //
    //  用于 PropertyDefinition.VisibleWhen 表达式的运行时求值。
    //  让编辑器能根据当前属性值动态显示/隐藏某些属性。
    //
    //  语法规则：
    //  - 单个比较：  "key == value"  "key != value"  "key > 1"
    //  - OR 组合：   "key == A || key == B"
    //  - AND 组合：  "key == A && count > 1"
    //  - 空表达式：   返回 true（始终可见）
    //
    //  求值顺序（优先级从低到高）：
    //  1. || (OR)——最低优先级
    //  2. && (AND)
    //  3. 比较操作符 (==, !=, >, <, >=, <=)
    //
    //  示例：
    //  - "tempoType == Interval"           → tempoType 为 Interval 时可见
    //  - "waves > 1"                       → waves 大于 1 时可见
    //  - "action == LookAt || action == Follow" → 任一满足即可见
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// VisibleWhen 条件表达式评估器——根据 PropertyBag 中的当前值判断属性是否可见。
    /// <para>
    /// 支持的操作符：==, !=, &gt;, &lt;, &gt;=, &lt;=, ||, &amp;&amp;
    /// </para>
    /// </summary>
    public static class VisibleWhenEvaluator
    {
        /// <summary>
        /// 评估条件表达式。
        /// <para>null 或空字符串返回 true（始终可见）。</para>
        /// </summary>
        /// <param name="expression">条件表达式，如 "tempoType == Interval"</param>
        /// <param name="bag">当前节点的属性值</param>
        /// <returns>true 表示属性应该可见，false 表示应该隐藏</returns>
        public static bool Evaluate(string? expression, PropertyBag bag)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return true;

            expression = expression!.Trim();

            // 递归下降解析：先拆 ||（最低优先级），再拆 &&，最后处理单个比较

            // 第 1 层：处理 || (OR)——优先级最低
            int orIndex = FindOperator(expression, "||");
            if (orIndex >= 0)
            {
                string left = expression.Substring(0, orIndex).Trim();
                string right = expression.Substring(orIndex + 2).Trim();
                return Evaluate(left, bag) || Evaluate(right, bag);
            }

            // 第 2 层：处理 && (AND)
            int andIndex = FindOperator(expression, "&&");
            if (andIndex >= 0)
            {
                string left = expression.Substring(0, andIndex).Trim();
                string right = expression.Substring(andIndex + 2).Trim();
                return Evaluate(left, bag) && Evaluate(right, bag);
            }

            // 第 3 层：处理单个比较表达式（如 "key == value"）
            return EvaluateComparison(expression, bag);
        }

        /// <summary>
        /// 评估单个比较表达式（不含 || 和 &&）。
        /// <para>尝试按顺序匹配操作符，注意顺序很重要：
        /// != 必须在 == 之前，>= 必须在 > 之前，否则会被错误匹配。</para>
        /// </summary>
        private static bool EvaluateComparison(string expr, PropertyBag bag)
        {
            // 操作符顺序很重要：多字符操作符必须在单字符之前匹配
            string[] operators = { "!=", "==", ">=", "<=", ">", "<" };

            foreach (var op in operators)
            {
                int idx = expr.IndexOf(op, StringComparison.Ordinal);
                if (idx < 0) continue;

                // 拆分为“键名”和“期望值”
                string key = expr.Substring(0, idx).Trim();        // 如 "tempoType"
                string valueStr = expr.Substring(idx + op.Length).Trim(); // 如 "Interval"
                object? actual = bag.GetRaw(key);  // 从 PropertyBag 获取实际值

                return CompareValues(actual, valueStr, op);
            }

            // 没有操作符——将整个表达式视为布尔键名（如 "isElite" → 读取 bag["isElite"]）
            return bag.Get<bool>(expr);
        }

        /// <summary>
        /// 比较实际值和期望值。
        /// <para>优先尝试数值比较（支持 float 精度容差 1e-6），
        /// 如果不是数字则回退到字符串比较。</para>
        /// </summary>
        /// <param name="actual">PropertyBag 中的实际值（可能是 int/float/string 等）</param>
        /// <param name="expected">表达式中的期望值字符串</param>
        /// <param name="op">比较操作符</param>
        private static bool CompareValues(object? actual, string expected, string op)
        {
            // null 值处理：只有 != 返回 true
            if (actual == null)
                return op == "!=" ? !string.IsNullOrEmpty(expected) : false;

            string actualStr = actual.ToString() ?? "";

            // 优先尝试数值比较（两边都能解析为数字时）
            if (TryParseNumber(actualStr, out double actualNum) && TryParseNumber(expected, out double expectedNum))
            {
                switch (op)
                {
                    case "==": return Math.Abs(actualNum - expectedNum) < 1e-6; // 浮点数精度容差
                    case "!=": return Math.Abs(actualNum - expectedNum) >= 1e-6;
                    case ">":  return actualNum > expectedNum;
                    case "<":  return actualNum < expectedNum;
                    case ">=": return actualNum >= expectedNum;
                    case "<=": return actualNum <= expectedNum;
                }
            }

            // 回退到字符串比较（如枚举值 "Interval" == "Interval"）
            switch (op)
            {
                case "==": return string.Equals(actualStr, expected, StringComparison.Ordinal);
                case "!=": return !string.Equals(actualStr, expected, StringComparison.Ordinal);
                default:   return false; // >, < 等对字符串无意义
            }
        }

        /// <summary>尝试将字符串解析为 double</summary>
        private static bool TryParseNumber(string s, out double result)
        {
            return double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out result);
        }

        /// <summary>
        /// 在表达式中查找操作符位置，跳过括号内的内容。
        /// <para>返回 -1 表示未找到。括号内的操作符不会被匹配，
        /// 保证嵌套表达式的正确性。</para>
        /// </summary>
        private static int FindOperator(string expr, string op)
        {
            int depth = 0; // 括号深度
            for (int i = 0; i <= expr.Length - op.Length; i++)
            {
                char c = expr[i];
                if (c == '(') depth++;       // 进入括号
                else if (c == ')') depth--;  // 离开括号
                else if (depth == 0 && expr.Substring(i, op.Length) == op)
                    return i; // 只在括号外层匹配
            }
            return -1;
        }
    }
}
