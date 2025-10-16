using PdfCropper;

namespace PdfCropper.Cli;

/// <summary>
/// Simple console logger implementation.
/// </summary>
internal sealed class ConsoleLogger : IPdfCropLogger
{
    private readonly bool _verbose;

    public ConsoleLogger(bool verbose = false)
    {
        _verbose = verbose;
    }

    public void LogInfo(string message)
    {
        if (_verbose)
        {
            Console.WriteLine($"[INFO] {message}");
        }
    }

    public void LogWarning(string message)
    {
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
