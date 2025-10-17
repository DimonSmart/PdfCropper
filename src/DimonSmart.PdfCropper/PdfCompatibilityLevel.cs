using System;
using System.Collections.Generic;
using System.Text;
using iText.Kernel.Pdf;

namespace DimonSmart.PdfCropper;

/// <summary>
/// Represents supported PDF compatibility levels for the output document.
/// </summary>
public enum PdfCompatibilityLevel
{
    Pdf10,
    Pdf11,
    Pdf12,
    Pdf13,
    Pdf14,
    Pdf15,
    Pdf16,
    Pdf17,
    Pdf20
}

/// <summary>
/// Provides parsing and formatting helpers for <see cref="PdfCompatibilityLevel"/> values.
/// </summary>
public static class PdfCompatibilityLevelInfo
{
    private static readonly string[] Versions =
    {
        "1.0",
        "1.1",
        "1.2",
        "1.3",
        "1.4",
        "1.5",
        "1.6",
        "1.7",
        "2.0"
    };

    private static readonly IReadOnlyList<string> ReadOnlyVersions = Array.AsReadOnly(Versions);

    private static readonly Dictionary<string, PdfCompatibilityLevel> VersionMap = new(StringComparer.Ordinal)
    {
        { "1.0", PdfCompatibilityLevel.Pdf10 },
        { "1.1", PdfCompatibilityLevel.Pdf11 },
        { "1.2", PdfCompatibilityLevel.Pdf12 },
        { "1.3", PdfCompatibilityLevel.Pdf13 },
        { "1.4", PdfCompatibilityLevel.Pdf14 },
        { "1.5", PdfCompatibilityLevel.Pdf15 },
        { "1.6", PdfCompatibilityLevel.Pdf16 },
        { "1.7", PdfCompatibilityLevel.Pdf17 },
        { "2.0", PdfCompatibilityLevel.Pdf20 }
    };

    /// <summary>
    /// Gets the list of version strings accepted by the parser.
    /// </summary>
    public static IReadOnlyList<string> SupportedVersions => ReadOnlyVersions;

    /// <summary>
    /// Attempts to parse a textual representation of a PDF compatibility level.
    /// </summary>
    public static bool TryParse(string? value, out PdfCompatibilityLevel level)
    {
        level = default;
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return false;
        }

        return VersionMap.TryGetValue(normalized, out level);
    }

    /// <summary>
    /// Formats the compatibility level as a canonical PDF version string.
    /// </summary>
    public static string ToVersionString(this PdfCompatibilityLevel level)
    {
        return level switch
        {
            PdfCompatibilityLevel.Pdf10 => "1.0",
            PdfCompatibilityLevel.Pdf11 => "1.1",
            PdfCompatibilityLevel.Pdf12 => "1.2",
            PdfCompatibilityLevel.Pdf13 => "1.3",
            PdfCompatibilityLevel.Pdf14 => "1.4",
            PdfCompatibilityLevel.Pdf15 => "1.5",
            PdfCompatibilityLevel.Pdf16 => "1.6",
            PdfCompatibilityLevel.Pdf17 => "1.7",
            PdfCompatibilityLevel.Pdf20 => "2.0",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unsupported compatibility level")
        };
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (ch == '_' || ch == '-' || ch == '/')
            {
                builder.Append('.');
                continue;
            }

            if (char.IsDigit(ch) || ch == '.')
            {
                builder.Append(ch);
            }
        }

        var normalized = builder.ToString().Trim('.');
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        return normalized;
    }
}

internal static class PdfCompatibilityLevelExtensions
{
    public static PdfVersion ToPdfVersion(this PdfCompatibilityLevel level)
    {
        return level switch
        {
            PdfCompatibilityLevel.Pdf10 => PdfVersion.PDF_1_0,
            PdfCompatibilityLevel.Pdf11 => PdfVersion.PDF_1_1,
            PdfCompatibilityLevel.Pdf12 => PdfVersion.PDF_1_2,
            PdfCompatibilityLevel.Pdf13 => PdfVersion.PDF_1_3,
            PdfCompatibilityLevel.Pdf14 => PdfVersion.PDF_1_4,
            PdfCompatibilityLevel.Pdf15 => PdfVersion.PDF_1_5,
            PdfCompatibilityLevel.Pdf16 => PdfVersion.PDF_1_6,
            PdfCompatibilityLevel.Pdf17 => PdfVersion.PDF_1_7,
            PdfCompatibilityLevel.Pdf20 => PdfVersion.PDF_2_0,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unsupported compatibility level")
        };
    }
}
