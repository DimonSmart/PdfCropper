using Microsoft.Extensions.Logging;

namespace PdfCropper.Cli;

/// <summary>
/// Simple console logger implementation using standard Microsoft.Extensions.Logging abstractions.
/// </summary>
internal sealed class ConsoleLogger(LogLevel minimumLevel) : IPdfCropLogger
{
    public void LogInfo(string message)
    {
        if (!IsEnabled(LogLevel.Information)) return;

        Console.WriteLine($"[INFO] {message}");
    }

    public void LogWarning(string message)
    {
        if (!IsEnabled(LogLevel.Warning)) return;

        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARN] {message}");
        Console.ForegroundColor = oldColor;
    }

    public void LogError(string message)
    {
        if (!IsEnabled(LogLevel.Error)) return;

        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"[ERROR] {message}");
        Console.ForegroundColor = oldColor;
    }

    private bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= minimumLevel;
    }
}
