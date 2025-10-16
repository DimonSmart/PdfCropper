using PdfCropper;

namespace PdfCropper.Cli;

/// <summary>
/// Simple console logger implementation.
/// </summary>
internal enum LogLevel
{
    None,
    Info
}

internal sealed class ConsoleLogger : IPdfCropLogger
{
    private readonly LogLevel _level;

    public ConsoleLogger(LogLevel level)
    {
        _level = level;
    }

    public void LogInfo(string message)
    {
        if (_level < LogLevel.Info) return;
        
        Console.WriteLine($"[INFO] {message}");
    }

    public void LogWarning(string message)
    {
        if (_level == LogLevel.None) return;

        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARN] {message}");
        Console.ForegroundColor = oldColor;
    }

    public void LogError(string message)
    {
        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"[ERROR] {message}");
        Console.ForegroundColor = oldColor;
    }
}
