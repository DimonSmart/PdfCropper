using DimonSmart.PdfCropper;

namespace DimonSmart.PdfCropper.PdfFontSubsetMerger;

public readonly record struct FontMergeLogEvent(FontMergeLogEventId Id, FontMergeLogLevel Level, string Message)
{
    public async Task LogAsync(IPdfCropLogger? logger)
    {
        if (logger == null)
        {
            return;
        }

        var formattedMessage = $"[FontSubsetMerge][{Id}] {Message}";
        switch (Level)
        {
            case FontMergeLogLevel.Info:
                await logger.LogInfoAsync(formattedMessage).ConfigureAwait(false);
                break;
            case FontMergeLogLevel.Warning:
                await logger.LogWarningAsync(formattedMessage).ConfigureAwait(false);
                break;
            case FontMergeLogLevel.Error:
                await logger.LogErrorAsync(formattedMessage).ConfigureAwait(false);
                break;
            default:
                await logger.LogInfoAsync(formattedMessage).ConfigureAwait(false);
                break;
        }
    }
}
