using System;
using System.Collections.Generic;
using System.Globalization;
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
        LogLevel logLevel)
    {
        InputPath = inputPath;
        OutputPath = outputPath;
        CropSettings = cropSettings;
        OptimizationSettings = optimizationSettings;
        LogLevel = logLevel;
    }

    public string InputPath { get; }

    public string OutputPath { get; }

    public CropSettings CropSettings { get; }

    public PdfOptimizationSettings OptimizationSettings { get; }

    public LogLevel LogLevel { get; }
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

        int? compressionLevel = null;
        var enableFullCompression = false;
        var enableSmartMode = false;
        var removeUnusedObjects = false;
        var removeXmpMetadata = false;
        var clearDocumentInfo = false;
        var removeEmbeddedStandardFonts = false;
        var infoKeys = new List<string>();
        var logLevel = LogLevel.None;

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

                case "--compression-level":
                    if (i + 1 >= args.Length)
                    {
                        return CommandLineParseResult.Failure("--compression-level requires a value");
                    }

                    if (!TryParseCompressionLevel(args[++i], out var parsedLevel))
                    {
                        return CommandLineParseResult.Failure($"invalid compression level. Use {SupportedCompressionLevels}.");
                    }

                    compressionLevel = parsedLevel;
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

                default:
                    return CommandLineParseResult.Failure($"Unknown argument '{current}'");
            }
        }

        var cropSettings = new CropSettings(method, excludeEdge, margin);
        var optimizationSettings = new PdfOptimizationSettings(
            compressionLevel,
            enableFullCompression,
            enableSmartMode,
            removeUnusedObjects,
            removeXmpMetadata,
            clearDocumentInfo,
            clearDocumentInfo ? null : infoKeys,
            removeEmbeddedStandardFonts);

        var options = new CommandLineOptions(inputPath, outputPath, cropSettings, optimizationSettings, logLevel);
        return CommandLineParseResult.Ok(options);
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
}
