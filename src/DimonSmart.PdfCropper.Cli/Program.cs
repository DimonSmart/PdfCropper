using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using DimonSmart.PdfCropper;
using DimonSmart.PdfCropper.Cli;

// Set console encoding to UTF-8 to properly display Unicode characters
try
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.InputEncoding = Encoding.UTF8;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Console.OutputEncoding = new UTF8Encoding(false);
    }
}
catch
{
    // Fallback to default encoding if UTF-8 setup fails
}

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    var parseResult = CommandLineParser.Parse(args);
    if (!parseResult.Success)
    {
        if (!string.IsNullOrEmpty(parseResult.ErrorMessage))
        {
            Console.Error.WriteLine($"Error: {parseResult.ErrorMessage}");
        }

        if (parseResult.ShowUsage)
        {
            ShowUsage();
        }

        return 1;
    }

    var options = parseResult.Options!;
    var cropSettings = options.CropSettings;
    var optimizationSettings = options.OptimizationSettings;

    var inputPath = options.InputPath;
    var outputPath = options.OutputPath;
    var logLevel = options.LogLevel;

    var inputHasMask = BatchPlanner.ContainsGlobPattern(inputPath);
    var outputHasMask = BatchPlanner.ContainsGlobPattern(outputPath);

    if (inputHasMask)
    {
        var plan = BatchPlanner.CreatePlan(inputPath, outputPath);
        if (!plan.Success)
        {
            Console.Error.WriteLine($"Error: {plan.ErrorMessage}");
            return plan.ExitCode;
        }

        var logger = new ConsoleLogger(logLevel);
        foreach (var item in plan.Files)
        {
            logger.LogInfo($"Processing {item.InputPath} -> {item.OutputPath}");
            if (!File.Exists(item.InputPath))
            {
                Console.Error.WriteLine($"Error: Input file '{item.InputPath}' was not found.");
                return 2;
            }

            var exitCode = await CropFileAsync(item.InputPath, item.OutputPath, cropSettings, optimizationSettings, logger);
            if (exitCode != 0)
            {
                return exitCode;
            }
        }

        return 0;
    }

    if (outputHasMask)
    {
        Console.Error.WriteLine("Error: Output path cannot contain wildcards when input is a single file.");
        return 1;
    }

    if (!File.Exists(inputPath))
    {
        Console.Error.WriteLine($"Error: Input file '{inputPath}' was not found.");
        return 2;
    }

    var singleLogger = new ConsoleLogger(logLevel);
    return await CropFileAsync(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), cropSettings, optimizationSettings, singleLogger);
}

static void ShowUsage()
{
    Console.WriteLine("Usage: PdfCropper.Cli <input.pdf> <output.pdf> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --preset <name>      Apply a preset of crop and optimization options");
    Console.WriteLine($"                        Available: {string.Join(", ", PdfCropProfiles.Keys)}");
    Console.WriteLine("  -m, --method <mode>   Cropping mode:");
    Console.WriteLine("                        0  = ContentBased (default, analyzes PDF content)");
    Console.WriteLine("                        00 = ContentBased with edge-touching content included");
    Console.WriteLine("                        01 = ContentBased excluding content touching page edges");
    Console.WriteLine("                        1  = BitmapBased (renders to image, slower but more accurate)");
    Console.WriteLine("  --margin <points>     Safety margin in points around content (default: 0.5)");
    Console.WriteLine("  --compression-level <level>  Deflate compression level (NO_COMPRESSION, DEFAULT_COMPRESSION, BEST_SPEED, BEST_COMPRESSION)");
    Console.WriteLine("                        Note: For maximum size reduction, combine with --full-compression, --smart, --remove-unused");
    Console.WriteLine("  --pdf-version <ver>   Target PDF compatibility (1.0-1.7, 2.0). Default: keep original version");
    Console.WriteLine("  --full-compression    Enable compact cross-reference compression");
    Console.WriteLine("  --smart               Enable smart mode resource deduplication");
    Console.WriteLine("  --remove-unused       Remove unused PDF objects before saving");
    Console.WriteLine("  --remove-xmp          Remove XMP metadata from the catalog");
    Console.WriteLine("  --clear-info          Remove legacy document info dictionary");
    Console.WriteLine("  --remove-info-key <k> Remove specific document info key (repeatable)");
    Console.WriteLine("  --remove-standard-fonts  Remove embedded files for standard PDF fonts");
    Console.WriteLine("  -v, --verbose         Enable verbose logging (alias for --log-level information)");
    Console.WriteLine("  -l, --log-level <lvl> Logging level:");
    Console.WriteLine("                        none = no logging (default)");
    Console.WriteLine("                        information = detailed processing info");
    Console.WriteLine("                        warning = warnings and errors only");
    Console.WriteLine("                        error = errors only");
    Console.WriteLine("                        debug = debug information");
    Console.WriteLine("                        trace = very detailed tracing");
    Console.WriteLine();
    Console.WriteLine("Input masks:");
    Console.WriteLine("  Use wildcards (e.g. scans/*.pdf) to crop multiple files at once.");
    Console.WriteLine("  Output can be a mask (out/*_CROP.pdf) or a directory path.");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  PdfCropper.Cli input.pdf output.pdf");
    Console.WriteLine("  PdfCropper.Cli input.pdf output.pdf --preset ebook");
    Console.WriteLine("  PdfCropper.Cli input.pdf output.pdf -m 1 -v");
    Console.WriteLine("  PdfCropper.Cli input.pdf output.pdf --margin 2.0");
    Console.WriteLine("  PdfCropper.Cli input.pdf output.pdf --preset aggressive -v");
    Console.WriteLine("  PdfCropper.Cli input.pdf output.pdf -m 01 --margin 1.5 -v");
    Console.WriteLine("  PdfCropper.Cli scans/*.pdf output/*_CROP.pdf");
    Console.WriteLine("  PdfCropper.Cli input.pdf output.pdf --compression-level BEST_COMPRESSION --full-compression --smart --remove-unused -v");
}

static async Task<int> CropFileAsync(
    string inputPath,
    string outputPath,
    CropSettings cropSettings,
    PdfOptimizationSettings optimizationSettings,
    ConsoleLogger logger)
{
    try
    {
        logger.LogInfo($"Reading input file: {inputPath}");
        var inputBytes = await File.ReadAllBytesAsync(inputPath);

        logger.LogInfo($"Cropping PDF using {cropSettings.Method} method...");
        if (cropSettings.Method == CropMethod.ContentBased && cropSettings.ExcludeEdgeTouchingObjects)
        {
            logger.LogInfo("Edge-touching content will be ignored during bounds detection");
        }

        var croppedBytes = await PdfSmartCropper.CropAsync(inputBytes, cropSettings, optimizationSettings, logger);

        var targetDirectory = Path.GetDirectoryName(outputPath);
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

static int MapErrorCode(PdfCropErrorCode code) => code switch
{
    PdfCropErrorCode.InvalidPdf => 10,
    PdfCropErrorCode.EncryptedPdf => 11,
    PdfCropErrorCode.ProcessingError => 12,
    _ => 13
};
