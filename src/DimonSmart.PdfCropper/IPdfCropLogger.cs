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
