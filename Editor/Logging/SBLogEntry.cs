#nullable enable

namespace SceneBlueprint.Editor.Logging
{
    /// <summary>
    /// 单条日志条目（值类型，减少 GC）。
    /// </summary>
    public readonly struct SBLogEntry
    {
        /// <summary>EditorApplication.timeSinceStartup</summary>
        public readonly double Timestamp;

        /// <summary>模块标识，如 "Pipeline", "Binding"</summary>
        public readonly string Tag;

        /// <summary>日志级别</summary>
        public readonly SBLogLevel Level;

        /// <summary>已格式化的消息文本</summary>
        public readonly string Message;

        /// <summary>可选上下文（关联对象名/ID，用于跳转定位）</summary>
        public readonly string? Context;

        /// <summary>帧号（区分同帧日志）</summary>
        public readonly int FrameCount;

        public SBLogEntry(double timestamp, string tag, SBLogLevel level,
            string message, string? context, int frameCount)
        {
            Timestamp = timestamp;
            Tag = tag;
            Level = level;
            Message = message;
            Context = context;
            FrameCount = frameCount;
        }

        /// <summary>格式化为导出文本行</summary>
        public string ToExportString()
        {
            var mins = (int)(Timestamp / 60);
            var secs = Timestamp - mins * 60;
            var levelStr = Level switch
            {
                SBLogLevel.Debug => "DBG",
                SBLogLevel.Info => "INF",
                SBLogLevel.Warning => "WRN",
                SBLogLevel.Error => "ERR",
                _ => "???",
            };
            var ctx = string.IsNullOrEmpty(Context) ? "" : $" ctx={Context}";
            return $"[{mins:D2}:{secs:00.00}] [{Tag}|{levelStr}] {Message}{ctx}";
        }
    }
}
