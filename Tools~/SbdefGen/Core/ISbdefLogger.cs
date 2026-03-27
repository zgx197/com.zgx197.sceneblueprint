namespace SbdefGen.Core;

/// <summary>
/// 日志抽象接口 — CLI 实现为 Console 输出，Unity 侧可适配为 Debug.Log。
/// </summary>
public interface ISbdefLogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}
