using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;
using DimonSmart.PdfCropper;

namespace DimonSmart.PdfCropper.Cli;

internal sealed class CommandLineOptions
{
    public CommandLineOptions(
        string inputPath,
        string outputPath,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings,
        LogLevel logLevel,
        bool mergeIntoSingleOutput,
        bool mergeFontSubsets)
    {
        InputPath = inputPath;
        OutputPath = outputPath;
        CropSettings = cropSettings;
        OptimizationSettings = optimizationSettings;
        LogLevel = logLevel;
        MergeIntoSingleOutput = mergeIntoSingleOutput;
        MergeFontSubsets = mergeFontSubsets;
    }

    public string InputPath { get; }

    public string OutputPath { get; }

    public CropSettings CropSettings { get; }

    public PdfOptimizationSettings OptimizationSettings { get; }

    public LogLevel LogLevel { get; }

    public bool MergeIntoSingleOutput { get; }

    public bool MergeFontSubsets { get; }
}

internal sealed class CommandLineParseResult
{
    private CommandLineParseResult(bool success, CommandLineOptions? options, string? errorMessage, bool showUsage)
    {
        Success = success;
        Options = options;
        ErrorMessage = errorMessage;
        ShowUsage = showUsage;
    }

    public bool Success { get; }

    public CommandLineOptions? Options { get; }

    public string? ErrorMessage { get; }

    public bool ShowUsage { get; }

    public static CommandLineParseResult Usage(string? error = null) => new(false, null, error, true);

    public static CommandLineParseResult Failure(string error) => new(false, null, error, false);

    public static CommandLineParseResult Ok(CommandLineOptions options) => new(true, options, null, false);
}

internal static class CommandLineParser
{
    private static readonly string SupportedCompressionLevels = string.Join(
        ", ",
        PdfCompressionLevels.Names);

    private static readonly string SupportedPdfVersions = string.Join(
        ", ",
        PdfCompatibilityLevelInfo.SupportedVersions);

    public static CommandLineParseResult Parse(string[] args)
    {
        if (args.Length < 2)
        {
            return CommandLineParseResult.Usage();
        }

        var inputPath = args[0];
        var outputPath = args[1];

        var method = CropSettings.Default.Method;
        var excludeEdge = CropSettings.Default.ExcludeEdgeTouchingObjects;
        var margin = CropSettings.Default.Margin;
        var edgeExclusionTolerance = CropSettings.Default.EdgeExclusionTolerance;
        var detectRepeated = CropSettings.Default.DetectRepeatedObjects;
        var repeatedThreshold = CropSettings.Default.RepeatedObjectOccurrenceThreshold;
        var repeatedMinimumPages = CropSettings.Default.RepeatedObjectMinimumPageCount;

        int? compressionLevel = null;
        var enableFullCompression = false;
        var enableSmartMode = false;
        var removeUnusedObjects = false;
        var removeXmpMetadata = false;
        var clearDocumentInfo = false;
        var removeEmbeddedStandardFonts = false;
        var infoKeys = new List<string>();
        var logLevel = LogLevel.None;
        PdfCompatibilityLevel? targetPdfVersion = null;
        var mergeIntoSingleOutput = false;
        var mergeFontSubsets = false;

        for (var i = 2; i < args.Length; i++)
        {
            var current = args[i];
            switch (current)
            {
                case "-v":
                case "--verbose":
                    logLevel = LogLevel.Information;
                    break;

                case "-l":
                case "--log-level":
                    if (i + 1 >= args.Length)
                    {
                        return CommandLineParseResult.Failure("--log-level requires a value (none, debug, trace, information, warning, error)");
                    }

                    var levelToken = args[++i];
                    if (!TryParseLogLevel(levelToken, out logLevel))
                    {
                        return CommandLineParseResult.Failure($"Invalid log level '{levelToken}'. Valid values: none, debug, trace, information, warning, error.");
                    }

                    break;

                case "-m":
                case "--method":
                    if (i + 1 >= args.Length)
                    {
                        return CommandLineParseResult.Failure("--method requires a value (0, 1, 00, or 01)");
                    }

                    if (!TryParseMethod(args[++i], out method, out excludeEdge))
                    {
                        return CommandLineParseResult.Failure("method must be 0, 1, 00, or 01");
                    }

                    break;

                case "--margin":
                    if (i + 1 >= args.Length)
                    {
                        return CommandLineParseResult.Failure("--margin requires a numeric value (margin in points)");
                    }

                    if (!float.TryParse(args[++i], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out margin) || margin < 0)
                    {
                        return CommandLineParseResult.Failure("margin must be a non-negative number");
                    }

                    break;

                case "--detect-repeated-objects":
                    if (i + 1 >= args.Length)
                    {
                        return CommandLineParseResult.Failure("--detect-repeated-objects requires a value (on or off)");
                    }

                    var detectRepeatedValue = args[++i];
                    if (!TryParseOnOff(detectRepeatedValue, out detectRepeated))
                    {
                        return CommandLineParseResult.Failure("--detect-repeated-objects value must be 'on' or 'off'");
                    }

                    break;

                case "--repeated-threshold":
                    if (i + 1 >= args.Length)
                    {
                        return CommandLineParseResult.Failure("--repeated-threshold requires a percentage value between 0 and 100");
                    }

                    if (!double.TryParse(args[++i], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out repeatedThreshold) ||
                        repeatedThreshold <= 0 || repeatedThreshold > 100)
                    {
                        return CommandLineParseResult.Failure("repeated threshold must be a number greater than 0 and less than or equal to 100");
                    }

                    break;

                case "--repeated-min-pages":
                    if (i + 1 >= args.Length)
                    {
                        return CommandLineParseResult.Failure("--repeated-min-pages requires a numeric value");
                    }

                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out repeatedMinimumPages) || repeatedMinimumPages < 2)
                    {
                        return CommandLineParseResult.Failure("repeated minimum pages must be an integer greater than or equal to 2");
                    }

                    break;

                case "--preset":
                    if (i + 1 >= args.Length)
                    {
                        return CommandLineParseResult.Failure($"--preset requires a value ({string.Join(", ", PdfCropProfiles.Keys)})");
                    }

                    var presetKey = args[++i];
                    if (!PdfCropProfiles.TryGet(presetKey, out var presetProfile))
                    {
                        return CommandLineParseResult.Failure($"Unknown preset '{presetKey}'. Use one of: {string.Join(", ", PdfCropProfiles.Keys)}.");
                    }

                    method = presetProfile.CropSettings.Method;
                    excludeEdge = presetProfile.CropSettings.ExcludeEdgeTouchingObjects;
                    margin = presetProfile.CropSettings.Margin;
                    edgeExclusionTolerance = presetProfile.CropSettings.EdgeExclusionTolerance;
                    detectRepeated = presetProfile.CropSettings.DetectRepeatedObjects;
                    repeatedThreshold = presetProfile.CropSettings.RepeatedObjectOccurrenceThreshold;
                    repeatedMinimumPages = presetProfile.CropSettings.RepeatedObjectMinimumPageCount;

                    var presetOptimization = presetProfile.OptimizationSettings;
                    compressionLevel = presetOptimization.CompressionLevel;
                    enableFullCompression = presetOptimization.EnableFullCompression;
                    enableSmartMode = presetOptimization.EnableSmartMode;
                    removeUnusedObjects = presetOptimization.RemoveUnusedObjects;
                    removeXmpMetadata = presetOptimization.RemoveXmpMetadata;
                    clearDocumentInfo = presetOptimization.ClearDocumentInfo;
                    removeEmbeddedStandardFonts = presetOptimization.RemoveEmbeddedStandardFonts;
                    targetPdfVersion = presetOptimization.TargetPdfVersion;
                    mergeFontSubsets = presetOptimization.MergeDuplicateFontSubsets;

                    infoKeys.Clear();
                    if (!clearDocumentInfo)
                    {
                        foreach (var key in presetOptimization.DocumentInfoKeysToRemove)
                        {
                            infoKeys.Add(key);
                        }
                    }

                    break;

                case "--compression-level":
                    if (i + 1 >= args.Length)
                    {
                        return CommandLineParseResult.Failure("--compression-level requires a value");
                    }

                    var compressionArg = args[++i];
                    if (!TryParseCompressionLevel(compressionArg, out var parsedLevel))
                    {
                        return CommandLineParseResult.Failure($"invalid compression level '{compressionArg}'. Use {SupportedCompressionLevels}.");
                    }

                    compressionLevel = parsedLevel;
                    break;

                case "--pdf-version":
                    if (i + 1 >= args.Length)
                    {
                        return CommandLineParseResult.Failure("--pdf-version requires a value");
                    }

                    var versionArg = args[++i];
                    if (!TryParsePdfVersion(versionArg, out var parsedVersion))
                    {
                        return CommandLineParseResult.Failure($"invalid PDF version '{versionArg}'. Use {SupportedPdfVersions}.");
                    }

                    targetPdfVersion = parsedVersion;
                    break;

                case "--full-compression":
                    enableFullCompression = true;
                    break;

                case "--smart":
                case "--smart-mode":
                    enableSmartMode = true;
                    break;

                case "--remove-unused":
                case "--remove-unused-objects":
                    removeUnusedObjects = true;
                    break;

                case "--remove-xmp":
                    removeXmpMetadata = true;
                    break;

                case "--clear-info":
                    clearDocumentInfo = true;
                    infoKeys.Clear();
                    break;

                case "--remove-info-key":
                    if (i + 1 >= args.Length)
                    {
                        return CommandLineParseResult.Failure("--remove-info-key requires a key name");
                    }

                    if (!clearDocumentInfo)
                    {
                        infoKeys.Add(args[++i]);
                        continue;
                    }

                    i++;
                    break;

                case "--remove-standard-fonts":
                    removeEmbeddedStandardFonts = true;
                    break;

                case "--merge-font-subsets":
                    mergeFontSubsets = true;
                    break;

                case "--merge":
                    mergeIntoSingleOutput = true;
                    break;

                default:
                    return CommandLineParseResult.Failure($"Unknown argument '{current}'");
            }
        }

        if (mergeIntoSingleOutput)
        {
            if (BatchPlanner.ContainsGlobPattern(outputPath))
            {
                return CommandLineParseResult.Failure("The --merge option requires a single output file path without wildcards. Provide a literal file name for the merged result.");
            }

            if (LooksLikeDirectory(outputPath))
            {
                return CommandLineParseResult.Failure("The --merge option produces one PDF file. Provide a destination file path instead of a directory.");
            }
        }

        var cropSettings = new CropSettings(
            method,
            excludeEdge,
            margin,
            edgeExclusionTolerance,
            detectRepeatedObjects: detectRepeated,
            repeatedObjectOccurrenceThreshold: repeatedThreshold,
            repeatedObjectMinimumPageCount: repeatedMinimumPages);
        var optimizationSettings = new PdfOptimizationSettings(
            compressionLevel,
            enableFullCompression,
            enableSmartMode,
            removeUnusedObjects,
            removeXmpMetadata,
            clearDocumentInfo,
            clearDocumentInfo ? null : infoKeys,
            removeEmbeddedStandardFonts,
            targetPdfVersion,
            mergeDuplicateFontSubsets: mergeFontSubsets);

        var options = new CommandLineOptions(
            inputPath,
            outputPath,
            cropSettings,
            optimizationSettings,
            logLevel,
            mergeIntoSingleOutput,
            mergeFontSubsets);
        return CommandLineParseResult.Ok(options);
    }

    private static bool LooksLikeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return true;
        }

        return Directory.Exists(path);
    }

    private static bool TryParseCompressionLevel(string value, out int level)
    {
        level = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (PdfCompressionLevels.TryGetValue(value, out level))
        {
            return true;
        }

        return false;
    }

    private static bool TryParsePdfVersion(string value, out PdfCompatibilityLevel version)
    {
        return PdfCompatibilityLevelInfo.TryParse(value, out version);
    }

    private static bool TryParseLogLevel(string value, out LogLevel logLevel)
    {
        logLevel = LogLevel.None;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "none":
                logLevel = LogLevel.None;
                return true;
            case "info":
            case "information":
                logLevel = LogLevel.Information;
                return true;
            case "warning":
            case "warn":
                logLevel = LogLevel.Warning;
                return true;
            case "error":
                logLevel = LogLevel.Error;
                return true;
            case "debug":
                logLevel = LogLevel.Debug;
                return true;
            case "trace":
                logLevel = LogLevel.Trace;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseMethod(string value, out CropMethod method, out bool excludeEdgeTouching)
    {
        method = CropSettings.Default.Method;
        excludeEdgeTouching = CropSettings.Default.ExcludeEdgeTouchingObjects;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();

        if (normalized == "0" || normalized == "00")
        {
            method = CropMethod.ContentBased;
            excludeEdgeTouching = false;
            return true;
        }

        if (normalized == "01")
        {
            method = CropMethod.ContentBased;
            excludeEdgeTouching = true;
            return true;
        }

        if (normalized == "1")
        {
            method = CropMethod.BitmapBased;
            excludeEdgeTouching = false;
            return true;
        }

        return false;
    }

    private static bool TryParseOnOff(string value, out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "on":
            case "true":
            case "yes":
            case "1":
                result = true;
                return true;
            case "off":
            case "false":
            case "no":
            case "0":
                result = false;
                return true;
            default:
                return false;
        }
    }
}
