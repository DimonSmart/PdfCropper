namespace DimonSmart.PdfCropper;

/// <summary>
/// A null logger implementation that does nothing.
/// </summary>
internal sealed class NullLogger : IPdfCropLogger
{
    public static readonly NullLogger Instance = new();

    private NullLogger()
    {
    }

    public Task LogInfoAsync(string message)
    {
        return Task.CompletedTask;
    }

    public Task LogWarningAsync(string message)
    {
        return Task.CompletedTask;
    }

    public Task LogErrorAsync(string message)
    {
        return Task.CompletedTask;
    }
}
