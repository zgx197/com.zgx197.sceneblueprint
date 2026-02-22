#nullable enable

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 摄像机震动数据（由 CameraShakeSystem 解析后传递给外部处理器）。
    /// </summary>
    public struct CameraShakeData
    {
        /// <summary>震动强度（值越大幅度越大）</summary>
        public float Intensity;

        /// <summary>震动持续时间（秒）</summary>
        public float Duration;

        /// <summary>震动频率（值越大抖动越快）</summary>
        public float Frequency;
    }

    /// <summary>
    /// 摄像机震动处理器接口——运行时解释器与视觉表现层的桥梁。
    /// <para>
    /// CameraShakeSystem 解析节点属性后，通过此接口通知外部执行震动。
    /// 不同环境提供不同实现：
    /// - 编辑器测试场景：直接操作 Camera.main.transform（CameraShakeHandler）
    /// - 帧同步运行时：调用 Cinemachine Impulse 或自定义震动系统
    /// </para>
    /// </summary>
    public interface ICameraShakeHandler
    {
        /// <summary>开始摄像机震动</summary>
        void OnShakeStart(CameraShakeData data);

        /// <summary>停止摄像机震动（提前中断时调用）</summary>
        void OnShakeStop() { }
    }
}
