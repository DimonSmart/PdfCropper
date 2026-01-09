using Microsoft.Extensions.Logging;

namespace DimonSmart.PdfCropper.Cli;

/// <summary>
/// Simple console logger implementation using standard Microsoft.Extensions.Logging abstractions.
/// </summary>
internal sealed class ConsoleLogger(LogLevel minimumLevel, int? debugPageIndex = null) : IPdfCropLogger, IPdfCropDebugLogger
{
    private const int DefaultMaxObjectLogs = 20;

    public Task LogInfoAsync(string message)
    {
        if (!IsEnabled(LogLevel.Information)) return Task.CompletedTask;

        Console.WriteLine($"[INFO] {message}");
        return Task.CompletedTask;
    }

    public Task LogWarningAsync(string message)
    {
        if (!IsEnabled(LogLevel.Warning)) return Task.CompletedTask;

        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARN] {message}");
        Console.ForegroundColor = oldColor;
        return Task.CompletedTask;
    }

    public Task LogErrorAsync(string message)
    {
        if (!IsEnabled(LogLevel.Error)) return Task.CompletedTask;

        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"[ERROR] {message}");
        Console.ForegroundColor = oldColor;
        return Task.CompletedTask;
    }

    private bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= minimumLevel;
    }

    public bool ShouldLogDebugForPage(int pageIndex)
    {
        return debugPageIndex.HasValue && pageIndex == debugPageIndex.Value;
    }

    public int MaxObjectLogs => DefaultMaxObjectLogs;
}
