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
    
    // On Windows, also try to set the console code page to UTF-8
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
    if (args.Length < 2)
    {
        ShowUsage();
        return 1;
    }

    var inputPath = args[0];
    var outputPath = args[1];
    var settings = CropSettings.Default;
    var logLevel = LogLevel.None;

    for (var i = 2; i < args.Length; i++)
    {
        if (args[i] == "-v" || args[i] == "--verbose")
        {
            logLevel = LogLevel.Information;
            continue;
        }

        if (args[i] == "-l" || args[i] == "--log-level")
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("Error: --log-level requires a value (none, debug, trace, information, warning, error)");
                return 1;
            }

            try
            {
                var levelValue = args[i + 1].ToLowerInvariant();
                logLevel = levelValue switch
                {
                    "none" => LogLevel.None,
                    "info" or "information" => LogLevel.Information,
                    "warning" or "warn" => LogLevel.Warning,
                    "error" => LogLevel.Error,
                    "debug" => LogLevel.Debug,
                    "trace" => LogLevel.Trace,
                    _ => throw new ArgumentException($"Invalid log level '{args[i + 1]}'. Valid values: none, debug, trace, information, warning, error.")
                };
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
            i++;
            continue;
        }

        if (args[i] == "-m" || args[i] == "--method")
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("Error: --method requires a value (0, 1, 00, or 01)");
                return 1;
            }

            if (!TryParseMethod(args[i + 1], out settings))
            {
                Console.Error.WriteLine("Error: method must be 0, 1, 00, or 01");
                return 1;
            }

            i++;
            continue;
        }

        if (args[i] == "--margin")
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("Error: --margin requires a numeric value (margin in points)");
                return 1;
            }

            if (!float.TryParse(args[i + 1], out var margin) || margin < 0)
            {
                Console.Error.WriteLine("Error: margin must be a non-negative number");
                return 1;
            }

            settings = new CropSettings(settings.Method, settings.ExcludeEdgeTouchingObjects, margin);
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

        logger.LogInfo($"Cropping PDF using {settings.Method} method...");
        if (settings.Method == CropMethod.ContentBased && settings.ExcludeEdgeTouchingObjects)
        {
            logger.LogInfo("Edge-touching content will be ignored during bounds detection");
        }

        var croppedBytes = await PdfSmartCropper.CropAsync(inputBytes, settings, logger);

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
    Console.WriteLine("  -m, --method <mode>   Cropping mode:");
    Console.WriteLine("                        0  = ContentBased (default, analyzes PDF content)");
    Console.WriteLine("                        00 = ContentBased with edge-touching content included");
    Console.WriteLine("                        01 = ContentBased excluding content touching page edges");
    Console.WriteLine("                        1  = BitmapBased (renders to image, slower but more accurate)");
    Console.WriteLine("  --margin <points>     Safety margin in points around content (default: 0.5)");
    Console.WriteLine("  -v, --verbose         Enable verbose logging (alias for --log-level information)");
    Console.WriteLine("  -l, --log-level <lvl> Logging level:");
    Console.WriteLine("                        none = no logging (default)");
    Console.WriteLine("                        information = detailed processing info");
    Console.WriteLine("                        warning = warnings and errors only");
    Console.WriteLine("                        error = errors only");
    Console.WriteLine("                        debug = debug information");
    Console.WriteLine("                        trace = very detailed tracing");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  PdfCropper.Cli input.pdf output.pdf");
    Console.WriteLine("  PdfCropper.Cli input.pdf output.pdf -m 1 -v");
    Console.WriteLine("  PdfCropper.Cli input.pdf output.pdf --margin 2.0");
    Console.WriteLine("  PdfCropper.Cli input.pdf output.pdf -m 01 --margin 1.5 -v");
}

static bool TryParseMethod(string value, out CropSettings settings)
{
    settings = CropSettings.Default;

    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    var normalized = value.Trim();

    if (normalized == "0" || normalized == "00")
    {
        settings = new CropSettings(CropMethod.ContentBased);
        return true;
    }

    if (normalized == "01")
    {
        settings = new CropSettings(CropMethod.ContentBased, true);
        return true;
    }

    if (normalized == "1")
    {
        settings = new CropSettings(CropMethod.BitmapBased);
        return true;
    }

    return false;
}

static int MapErrorCode(PdfCropErrorCode code) => code switch
{
    PdfCropErrorCode.InvalidPdf => 10,
    PdfCropErrorCode.EncryptedPdf => 11,
    PdfCropErrorCode.ProcessingError => 12,
    _ => 13
};
