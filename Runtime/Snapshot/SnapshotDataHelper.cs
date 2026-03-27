#nullable enable
using System;

namespace SceneBlueprint.Runtime.Snapshot
{
    /// <summary>
    /// 快照数据类型转换辅助类 — 供 RestoreFromExportData 生成代码使用。
    /// <para>
    /// CollectExportData 输出的 value 是 object 类型，恢复时需要安全地转换为具体类型。
    /// 这些方法封装了 Convert/TryParse 逻辑，生成代码可以简洁地调用。
    /// </para>
    /// </summary>
    public static class SnapshotDataHelper
    {
        /// <summary>安全转换为 int</summary>
        public static int ToInt(object v) => Convert.ToInt32(v);

        /// <summary>安全转换为 float</summary>
        public static float ToFloat(object v) => Convert.ToSingle(v);

        /// <summary>安全转换为 bool</summary>
        public static bool ToBool(object v) => Convert.ToBoolean(v);

        /// <summary>安全转换为 string（null 安全）</summary>
        public static string ToStr(object? v) => v?.ToString() ?? "";

        /// <summary>安全转换为枚举类型（解析失败返回 default）</summary>
        public static T ToEnum<T>(object? v) where T : struct, Enum
            => v != null && Enum.TryParse<T>(v.ToString(), out var e) ? e : default;
    }
}
