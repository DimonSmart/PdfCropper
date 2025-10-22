namespace DimonSmart.PdfCropper;

/// <summary>
/// Logger interface for PDF cropping operations.
/// </summary>
public interface IPdfCropLogger
{
    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    Task LogInfoAsync(string message);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    Task LogWarningAsync(string message);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    Task LogErrorAsync(string message);
}

/// <summary>
/// Extension methods for <see cref="IPdfCropLogger"/> to provide synchronous logging.
/// </summary>
public static class PdfCropLoggerExtensions
{
    /// <summary>
    /// Logs an informational message synchronously.
    /// </summary>
    public static void LogInfo(this IPdfCropLogger logger, string message)
    {
        logger.LogInfoAsync(message).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Logs a warning message synchronously.
    /// </summary>
    public static void LogWarning(this IPdfCropLogger logger, string message)
    {
        logger.LogWarningAsync(message).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Logs an error message synchronously.
    /// </summary>
    public static void LogError(this IPdfCropLogger logger, string message)
    {
        logger.LogErrorAsync(message).GetAwaiter().GetResult();
    }
}
