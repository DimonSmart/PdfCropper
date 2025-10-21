using DimonSmart.PdfCropper;

namespace DimonSmart.PdfCropper.PdfFontSubsetMerger;

public readonly record struct FontMergeLogEvent(int Id, FontMergeLogLevel Level, string Message)
{
    public void Log(IPdfCropLogger logger)
    {
        if (logger == null)
        {
            return;
        }

        var formattedMessage = $"[FontSubsetMerge][{Id}] {Message}";
        switch (Level)
        {
            case FontMergeLogLevel.Info:
                logger.LogInfo(formattedMessage);
                break;
            case FontMergeLogLevel.Warning:
                logger.LogWarning(formattedMessage);
                break;
            case FontMergeLogLevel.Error:
                logger.LogError(formattedMessage);
                break;
            default:
                logger.LogInfo(formattedMessage);
                break;
        }
    }
}
