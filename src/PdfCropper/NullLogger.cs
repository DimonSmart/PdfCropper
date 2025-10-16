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

    public void LogInfo(string message)
    {
    }

    public void LogWarning(string message)
    {
    }

    public void LogError(string message)
    {
    }
}
