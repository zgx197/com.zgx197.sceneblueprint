#nullable enable
using System.Collections.Generic;
using UnityEditor;

namespace SceneBlueprint.Editor.Logging
{
    /// <summary>
    /// 日志系统配置，持久化到 EditorPrefs。
    /// </summary>
    public static class SBLogSettings
    {
        private const string PrefixKey = "SBLog.";

        // ─── 全局级别 ───

        private const string GlobalLevelKey = PrefixKey + "GlobalLevel";

        public static SBLogLevel GlobalLevel
        {
            get => (SBLogLevel)EditorPrefs.GetInt(GlobalLevelKey, (int)SBLogLevel.Info);
            set => EditorPrefs.SetInt(GlobalLevelKey, (int)value);
        }

        // ─── 缓冲区容量 ───

        private const string BufferCapacityKey = PrefixKey + "BufferCapacity";

        public static int BufferCapacity
        {
            get => EditorPrefs.GetInt(BufferCapacityKey, 500);
            set => EditorPrefs.SetInt(BufferCapacityKey, value);
        }

        // ─── 静默 Console ───

        private const string MuteConsoleKey = PrefixKey + "MuteConsole";

        /// <summary>为 true 时只写缓冲区，不输出到 Unity Console</summary>
        public static bool MuteConsole
        {
            get => EditorPrefs.GetBool(MuteConsoleKey, false);
            set => EditorPrefs.SetBool(MuteConsoleKey, value);
        }

        // ─── 模块开关 ───

        private const string TagEnabledPrefix = PrefixKey + "TagEnabled.";

        /// <summary>查询指定 Tag 是否启用（默认启用）</summary>
        public static bool IsTagEnabled(string tag)
            => EditorPrefs.GetBool(TagEnabledPrefix + tag, true);

        /// <summary>设置指定 Tag 的启用状态</summary>
        public static void SetTagEnabled(string tag, bool enabled)
            => EditorPrefs.SetBool(TagEnabledPrefix + tag, enabled);

        /// <summary>获取所有预定义 Tag 列表</summary>
        public static IReadOnlyList<string> GetAllPredefinedTags()
        {
            return new[]
            {
                SBLogTags.Blueprint,
                SBLogTags.Binding,
                SBLogTags.Selection,
                SBLogTags.Pipeline,
                SBLogTags.Marker,
                SBLogTags.Validator,
                SBLogTags.Export,
                SBLogTags.Layer,
            };
        }

        /// <summary>启用所有预定义 Tag</summary>
        public static void EnableAllTags()
        {
            foreach (var tag in GetAllPredefinedTags())
                SetTagEnabled(tag, true);
        }

        /// <summary>静默所有预定义 Tag</summary>
        public static void MuteAllTags()
        {
            foreach (var tag in GetAllPredefinedTags())
                SetTagEnabled(tag, false);
        }

        /// <summary>重置所有设置为默认值</summary>
        public static void ResetToDefaults()
        {
            GlobalLevel = SBLogLevel.Info;
            BufferCapacity = 500;
            MuteConsole = false;
            EnableAllTags();
        }
    }
}
