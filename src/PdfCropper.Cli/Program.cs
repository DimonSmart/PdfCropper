using PdfCropper;
using PdfCropper.Cli;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length < 2)
    {
        ShowUsage();
        return 1;
    }

    var inputPath = args[0];
    var outputPath = args[1];
    var method = CropMethod.ContentBased;
    var logLevel = LogLevel.None;

    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "-v" || args[i] == "--verbose")
        {
            logLevel = LogLevel.Info;
            continue;
        }

        if (args[i] == "-l" || args[i] == "--log-level")
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("Error: --log-level requires a value (none or info)");
                return 1;
            }

            var levelValue = args[i + 1].ToLowerInvariant();
            if (levelValue != "none" && levelValue != "info")
            {
                Console.Error.WriteLine($"Error: Invalid log level '{args[i + 1]}'. Use 'none' or 'info'.");
                return 1;
            }

            logLevel = levelValue == "info" ? LogLevel.Info : LogLevel.None;
            i++;
            continue;
        }

        if (args[i] == "-m" || args[i] == "--method")
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
            i++;
            continue;
        }

        Console.Error.WriteLine($"Error: Unknown argument '{args[i]}'");
        return 1;
    }

    if (!File.Exists(inputPath))
    {
        Console.Error.WriteLine($"Error: Input file '{inputPath}' was not found.");
        return 2;
    }

    var logger = new ConsoleLogger(logLevel);

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
        logger.LogError($"Stack trace: {ex.StackTrace}");
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
    Console.WriteLine("  -v, --verbose         Enable verbose logging (alias for --log-level info)");
    Console.WriteLine("  -l, --log-level <lvl> Logging level: none (default) or info");
    Console.WriteLine("                        info = per-page sizes and timing details");
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
