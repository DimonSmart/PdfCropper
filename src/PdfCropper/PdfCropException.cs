namespace DimonSmart.PdfCropper;

public class PdfCropException : Exception
{
    public PdfCropErrorCode Code { get; }

    public PdfCropException()
    {
    }

    public PdfCropException(PdfCropErrorCode code)
        : this(code, GetMessage(code))
    {
    }

    public PdfCropException(PdfCropErrorCode code, string? message)
        : base(message)
    {
        Code = code;
    }

    public PdfCropException(PdfCropErrorCode code, string? message, Exception? innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    private static string GetMessage(PdfCropErrorCode code) => code switch
    {
        PdfCropErrorCode.InvalidPdf => "Invalid or corrupted PDF document.",
        PdfCropErrorCode.EncryptedPdf => "The PDF document is encrypted and cannot be processed.",
        PdfCropErrorCode.ProcessingError => "An error occurred while processing the PDF document.",
        _ => "An unknown PDF crop error occurred."
    };
}
