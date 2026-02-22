#nullable enable
using System;
using UnityEngine;
using UnityEditor;

namespace SceneBlueprint.Editor.Logging
{
    /// <summary>
    /// SceneBlueprint 统一日志入口。
    /// <para>
    /// 所有模块通过此静态类输出日志，内部负责：
    /// 级别过滤 → 模块开关过滤 → 格式化 → 写入环形缓冲区 → 输出到 Unity Console。
    /// </para>
    /// <example>
    /// SBLog.Info(SBLogTags.Blueprint, "已加载: {0} (节点: {1})", path, count);
    /// SBLog.Debug(SBLogTags.Selection, "选中变更: {0}", go.name);
    /// SBLog.Warn(SBLogTags.Validator, "标记 {0} 未找到", markerId);
    /// </example>
    /// </summary>
    public static class SBLog
    {
        private static SBLogBuffer? _buffer;

        /// <summary>全局环形缓冲区（懒初始化）</summary>
        public static SBLogBuffer Buffer
        {
            get
            {
                if (_buffer == null)
                {
                    _buffer = new SBLogBuffer(SBLogSettings.BufferCapacity);
                }
                return _buffer;
            }
        }

        /// <summary>每次有新日志写入时触发（SBLogWindow 监听此事件）</summary>
        public static event Action<SBLogEntry>? OnLogEntry;

        // ─── 便捷 API ───

        public static void Debug(string tag, string message, string? context = null)
            => Log(SBLogLevel.Debug, tag, message, context);

        public static void Debug(string tag, string format, params object[] args)
            => Log(SBLogLevel.Debug, tag, string.Format(format, args), null);

        public static void Info(string tag, string message, string? context = null)
            => Log(SBLogLevel.Info, tag, message, context);

        public static void Info(string tag, string format, params object[] args)
            => Log(SBLogLevel.Info, tag, string.Format(format, args), null);

        public static void Warn(string tag, string message, string? context = null)
            => Log(SBLogLevel.Warning, tag, message, context);

        public static void Warn(string tag, string format, params object[] args)
            => Log(SBLogLevel.Warning, tag, string.Format(format, args), null);

        public static void Error(string tag, string message, string? context = null)
            => Log(SBLogLevel.Error, tag, message, context);

        public static void Error(string tag, string format, params object[] args)
            => Log(SBLogLevel.Error, tag, string.Format(format, args), null);

        // ─── 核心方法 ───

        /// <summary>写入一条日志</summary>
        public static void Log(SBLogLevel level, string tag, string message, string? context = null)
        {
            // 1. 全局级别门槛
            if (level < SBLogSettings.GlobalLevel) return;

            // 2. 模块开关
            if (!SBLogSettings.IsTagEnabled(tag)) return;

            // 3. 构造条目
            var entry = new SBLogEntry(
                timestamp: EditorApplication.timeSinceStartup,
                tag: tag,
                level: level,
                message: message,
                context: context,
                frameCount: Time.frameCount
            );

            // 4. 写入环形缓冲区
            Buffer.Push(entry);

            // 5. 触发事件（窗口监听）
            OnLogEntry?.Invoke(entry);

            // 6. 输出到 Unity Console（除非静默）
            if (!SBLogSettings.MuteConsole)
            {
                var consoleMsg = $"[SB.{tag}] {message}";
                switch (level)
                {
                    case SBLogLevel.Debug:
                    case SBLogLevel.Info:
                        UnityEngine.Debug.Log(consoleMsg);
                        break;
                    case SBLogLevel.Warning:
                        UnityEngine.Debug.LogWarning(consoleMsg);
                        break;
                    case SBLogLevel.Error:
                        UnityEngine.Debug.LogError(consoleMsg);
                        break;
                }
            }
        }

        /// <summary>重新初始化缓冲区（容量变更时调用）</summary>
        public static void ReinitBuffer()
        {
            _buffer = new SBLogBuffer(SBLogSettings.BufferCapacity);
        }
    }
}
