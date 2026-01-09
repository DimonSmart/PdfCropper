using System.Collections.Generic;
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
    var mergeIntoSingleOutput = options.MergeIntoSingleOutput;
    var debugPageIndex = options.DebugPageIndex;

    var inputHasMask = BatchPlanner.ContainsGlobPattern(inputPath);
    var outputHasMask = BatchPlanner.ContainsGlobPattern(outputPath);

    if (mergeIntoSingleOutput)
    {
        var logger = new ConsoleLogger(logLevel, debugPageIndex);
        IReadOnlyList<string> inputs;

        if (inputHasMask)
        {
            var discovery = BatchPlanner.DiscoverInputs(inputPath);
            if (!discovery.Success)
            {
                Console.Error.WriteLine($"Error: {discovery.ErrorMessage}");
                return discovery.ExitCode;
            }

            inputs = discovery.Files;
        }
        else
        {
            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine($"Error: Input file '{inputPath}' was not found.");
                return 2;
            }

            inputs = new[] { Path.GetFullPath(inputPath) };
        }

        if (inputs.Count == 0)
        {
            Console.Error.WriteLine("Error: No input files were found for merging.");
            return 2;
        }

        await logger.LogInfoAsync("Merging files in the following order:").ConfigureAwait(false);
        for (var i = 0; i < inputs.Count; i++)
        {
            await logger.LogInfoAsync($"  {i + 1}. {inputs[i]}").ConfigureAwait(false);
        }

        return await MergeFilesAsync(inputs, Path.GetFullPath(outputPath), cropSettings, optimizationSettings, logger);
    }

    if (inputHasMask)
    {
        var plan = BatchPlanner.CreatePlan(inputPath, outputPath);
        if (!plan.Success)
        {
            Console.Error.WriteLine($"Error: {plan.ErrorMessage}");
            return plan.ExitCode;
        }

        var logger = new ConsoleLogger(logLevel, debugPageIndex);
        foreach (var item in plan.Files)
        {
            await logger.LogInfoAsync($"Processing {item.InputPath} -> {item.OutputPath}").ConfigureAwait(false);
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

    var singleLogger = new ConsoleLogger(logLevel, debugPageIndex);
    return await CropFileAsync(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), cropSettings, optimizationSettings, singleLogger);
}

static void ShowUsage()
{
    Console.WriteLine("Usage: DimonSmart.PdfCropper.Cli.exe <input.pdf> <output.pdf> [options]");
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
    Console.WriteLine("  --detect-repeated-objects <on|off>  Exclude content repeated on most pages (content-based only)");
    Console.WriteLine("  --repeated-threshold <percent>  Minimum percentage of pages for repeated content (default: 40)");
    Console.WriteLine("  --repeated-min-pages <count>    Minimum document pages before detection (default: 3)");
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
    Console.WriteLine("  --merge-font-subsets  Merge duplicate font subset resources before other optimizations");
    Console.WriteLine("  --merge              Merge all inputs into a single output PDF");
    Console.WriteLine("                        Output must be a file path without wildcards");
    Console.WriteLine("  --debug-page <n>      Emit extended diagnostics for a single page (1-based index, enables info logging)");
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
    Console.WriteLine("  DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf");
    Console.WriteLine("  DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --preset ebook");
    Console.WriteLine("  DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf -m 1 -v");
    Console.WriteLine("  DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --margin 2.0");
    Console.WriteLine("  DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --preset aggressive -v");
    Console.WriteLine("  DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf -m 01 --margin 1.5 -v");
    Console.WriteLine("  DimonSmart.PdfCropper.Cli.exe scans/*.pdf output/*_CROP.pdf");
    Console.WriteLine("  DimonSmart.PdfCropper.Cli.exe scans/*.pdf merged/scans.pdf --merge");
    Console.WriteLine("  DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --compression-level BEST_COMPRESSION --full-compression --smart --remove-unused -v");
    Console.WriteLine("  DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --debug-page 2");
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
        await logger.LogInfoAsync($"Reading input file: {inputPath}").ConfigureAwait(false);
        var inputBytes = await File.ReadAllBytesAsync(inputPath);

        await logger.LogInfoAsync($"Cropping PDF using {cropSettings.Method} method...").ConfigureAwait(false);
        if (cropSettings.Method == CropMethod.ContentBased && cropSettings.ExcludeEdgeTouchingObjects)
        {
            await logger.LogInfoAsync("Edge-touching content will be ignored during bounds detection").ConfigureAwait(false);
        }

        var croppedBytes = await PdfSmartCropper.CropAsync(inputBytes, cropSettings, optimizationSettings, logger);

        var targetDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        await logger.LogInfoAsync($"Writing output file: {outputPath}").ConfigureAwait(false);
        await File.WriteAllBytesAsync(outputPath, croppedBytes);

        Console.WriteLine($"Success: PDF cropped and saved to {outputPath}");
        return 0;
    }
    catch (PdfCropException ex)
    {
        await logger.LogErrorAsync($"Cropping failed: {ex.Message}").ConfigureAwait(false);
        return MapErrorCode(ex.Code);
    }
    catch (Exception ex)
    {
        await logger.LogErrorAsync($"Unexpected error: {ex.Message}").ConfigureAwait(false);
        await logger.LogErrorAsync($"Stack trace: {ex.StackTrace}").ConfigureAwait(false);
        return 99;
    }
}

static async Task<int> MergeFilesAsync(
    IReadOnlyList<string> inputPaths,
    string outputPath,
    CropSettings cropSettings,
    PdfOptimizationSettings optimizationSettings,
    ConsoleLogger logger)
{
    try
    {
        var inputBytes = new List<byte[]>(inputPaths.Count);
        foreach (var input in inputPaths)
        {
            if (!File.Exists(input))
            {
                Console.Error.WriteLine($"Error: Input file '{input}' was not found.");
                return 2;
            }

            await logger.LogInfoAsync($"Reading input file: {input}").ConfigureAwait(false);
            inputBytes.Add(await File.ReadAllBytesAsync(input));
        }

        await logger.LogInfoAsync($"Cropping and merging {inputPaths.Count} document(s) using {cropSettings.Method} method...").ConfigureAwait(false);
        if (cropSettings.Method == CropMethod.ContentBased && cropSettings.ExcludeEdgeTouchingObjects)
        {
            await logger.LogInfoAsync("Edge-touching content will be ignored during bounds detection").ConfigureAwait(false);
        }

        var mergedBytes = await PdfSmartCropper.CropAndMergeAsync(inputBytes, cropSettings, optimizationSettings, logger);

        var targetDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        await logger.LogInfoAsync($"Writing output file: {outputPath}").ConfigureAwait(false);
        await File.WriteAllBytesAsync(outputPath, mergedBytes);

        Console.WriteLine($"Success: PDF cropped and saved to {outputPath}");
        return 0;
    }
    catch (PdfCropException ex)
    {
        await logger.LogErrorAsync($"Cropping failed: {ex.Message}").ConfigureAwait(false);
        return MapErrorCode(ex.Code);
    }
    catch (Exception ex)
    {
        await logger.LogErrorAsync($"Unexpected error: {ex.Message}").ConfigureAwait(false);
        await logger.LogErrorAsync($"Stack trace: {ex.StackTrace}").ConfigureAwait(false);
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
