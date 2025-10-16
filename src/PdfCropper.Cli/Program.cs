using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using PdfCropper;
using PdfCropper.Cli;

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
    var method = CropMethod.ContentBased;
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
                Console.Error.WriteLine("Error: --method requires a value (0 or 1)");
                return 1;
            }

            if (!int.TryParse(args[i + 1], out var methodValue) || (methodValue != 0 && methodValue != 1))
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
}

static int MapErrorCode(PdfCropErrorCode code) => code switch
{
    PdfCropErrorCode.InvalidPdf => 10,
    PdfCropErrorCode.EncryptedPdf => 11,
    PdfCropErrorCode.ProcessingError => 12,
    _ => 13
};
