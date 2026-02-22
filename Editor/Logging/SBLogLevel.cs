#nullable enable

namespace SceneBlueprint.Editor.Logging
{
    /// <summary>
    /// 日志级别。数值越大优先级越高。
    /// </summary>
    public enum SBLogLevel
    {
        /// <summary>开发调试细节（默认关闭）</summary>
        Debug = 0,

        /// <summary>正常操作记录</summary>
        Info = 1,

        /// <summary>潜在问题警告</summary>
        Warning = 2,

        /// <summary>错误</summary>
        Error = 3,
    }
}
