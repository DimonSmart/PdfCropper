using System;
using System.Collections.Generic;
using iText.Kernel.Pdf;

namespace DimonSmart.PdfCropper;

/// <summary>
/// Provides access to iText compression levels using canonical constant names.
/// </summary>
public static class PdfCompressionLevels
{
    public const string NoCompression = nameof(CompressionConstants.NO_COMPRESSION);
    public const string DefaultCompression = nameof(CompressionConstants.DEFAULT_COMPRESSION);
    public const string BestSpeed = nameof(CompressionConstants.BEST_SPEED);
    public const string BestCompression = nameof(CompressionConstants.BEST_COMPRESSION);

    private static readonly Dictionary<string, int> LevelMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { NoCompression, CompressionConstants.NO_COMPRESSION },
        { DefaultCompression, CompressionConstants.DEFAULT_COMPRESSION },
        { BestSpeed, CompressionConstants.BEST_SPEED },
        { BestCompression, CompressionConstants.BEST_COMPRESSION }
    };

    /// <summary>
    /// Attempts to resolve a compression level name to its numeric value.
    /// </summary>
    public static bool TryGetValue(string name, out int level)
    {
        level = default;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalized = name.Trim().Replace('-', '_');
        return LevelMap.TryGetValue(normalized, out level);
    }

    /// <summary>
    /// Gets the set of supported compression level names.
    /// </summary>
    public static IReadOnlyCollection<string> Names => LevelMap.Keys;
}
