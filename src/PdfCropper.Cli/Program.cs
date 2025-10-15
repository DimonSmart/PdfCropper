using PdfCropper;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine("Usage: PdfCropper.Cli <input.pdf> <output.pdf>");
        return 1;
    }

    var inputPath = args[0];
    var outputPath = args[1];

    if (!File.Exists(inputPath))
    {
        Console.Error.WriteLine($"Input file '{inputPath}' was not found.");
        return 2;
    }

    try
    {
        var inputBytes = await File.ReadAllBytesAsync(inputPath);
        var croppedBytes = await PdfSmartCropper.CropAsync(inputBytes);

        var targetDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        await File.WriteAllBytesAsync(outputPath, croppedBytes);
        return 0;
    }
    catch (PdfCropException ex)
    {
        Console.Error.WriteLine($"Cropping failed: {ex.Message}");
        return MapErrorCode(ex.Code);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Unexpected error: {ex.Message}");
        return 99;
    }
}

static int MapErrorCode(PdfCropErrorCode code) => code switch
{
    PdfCropErrorCode.InvalidPdf => 10,
    PdfCropErrorCode.EncryptedPdf => 11,
    PdfCropErrorCode.ProcessingError => 12,
    _ => 13
};
