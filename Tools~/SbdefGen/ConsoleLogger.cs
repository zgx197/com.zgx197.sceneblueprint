using System;
using SbdefGen.Core;

namespace SbdefGen;

/// <summary>CLI 环境下的日志实现，输出到标准输出/标准错误。</summary>
public sealed class ConsoleLogger : ISbdefLogger
{
    public bool Verbose { get; set; }

    public void Info(string message)
    {
        if (Verbose)
            Console.WriteLine(message);
    }

    public void Warn(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Error.WriteLine($"[WARN] {message}");
        Console.ResetColor();
    }

    public void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
    }
}
