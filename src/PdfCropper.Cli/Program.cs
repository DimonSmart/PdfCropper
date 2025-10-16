using PdfCropper;
using PdfCropper.Cli;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    // Parse arguments
    if (args.Length < 2 || args.Length > 4)
    {
        ShowUsage();
        return 1;
    }

    var inputPath = args[0];
    var outputPath = args[1];
    var method = CropMethod.ContentBased;
    var verbose = false;

    // Parse optional arguments
    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "-v" || args[i] == "--verbose")
        {
            verbose = true;
        }
        else if (args[i] == "-m" || args[i] == "--method")
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("Error: --method requires a value (0 or 1)");
                return 1;
            }
            
            if (!int.TryParse(args[i + 1], out int methodValue) || (methodValue != 0 && methodValue != 1))
            {
                Console.Error.WriteLine("Error: method must be 0 (ContentBased) or 1 (BitmapBased)");
                return 1;
            }
            
            method = (CropMethod)methodValue;
            i++; // Skip the next argument
        }
    }

    if (!File.Exists(inputPath))
    {
        Console.Error.WriteLine($"Error: Input file '{inputPath}' was not found.");
        return 2;
    }

    var logger = new ConsoleLogger(verbose);

    try
    {
        logger.LogInfo($"Reading input file: {inputPath}");
        var inputBytes = await File.ReadAllBytesAsync(inputPath);
        
        logger.LogInfo($"Cropping PDF using {method} method...");
        var croppedBytes = await PdfSmartCropper.CropAsync(inputBytes, method, logger);

        var targetDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        logger.LogInfo($"Writing output file: {outputPath}");
        await File.WriteAllBytesAsync(outputPath, croppedBytes);
        
        Console.WriteLine($"Success: PDF cropped and saved to {outputPath}");
        return 0;
    }
    catch (PdfCropException ex)
    {
        logger.LogError($"Cropping failed: {ex.Message}");
        return MapErrorCode(ex.Code);
    }
    catch (Exception ex)
    {
        logger.LogError($"Unexpected error: {ex.Message}");
        if (verbose)
        {
            logger.LogError($"Stack trace: {ex.StackTrace}");
        }
        return 99;
    }
}

static void ShowUsage()
{
    Console.WriteLine("Usage: PdfCropper.Cli <input.pdf> <output.pdf> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -m, --method <0|1>    Cropping method:");
    Console.WriteLine("                        0 = ContentBased (default, analyzes PDF content)");
    Console.WriteLine("                        1 = BitmapBased (renders to image, slower but more accurate)");
    Console.WriteLine("  -v, --verbose         Enable verbose logging");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  PdfCropper.Cli input.pdf output.pdf");
    Console.WriteLine("  PdfCropper.Cli input.pdf output.pdf -m 1 -v");
}

static int MapErrorCode(PdfCropErrorCode code) => code switch
{
    PdfCropErrorCode.InvalidPdf => 10,
    PdfCropErrorCode.EncryptedPdf => 11,
    PdfCropErrorCode.ProcessingError => 12,
    _ => 13
};
