using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DimonSmart.PdfCropper;

/// <summary>
/// Represents optimization settings applied while saving the cropped PDF document.
/// </summary>
public sealed class PdfOptimizationSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfOptimizationSettings"/> class.
    /// </summary>
    /// <param name="compressionLevel">Compression level for generated streams. <c>null</c> leaves library defaults.</param>
    /// <param name="enableFullCompression">Enables compact cross-reference compression when set.</param>
    /// <param name="enableSmartMode">Enables iText smart mode deduplication when set.</param>
    /// <param name="removeUnusedObjects">Removes unused PDF objects prior to saving when set.</param>
    /// <param name="removeXmpMetadata">Removes XMP metadata stream from the catalog when set.</param>
    /// <param name="clearDocumentInfo">Removes the legacy document info dictionary entirely when set.</param>
    /// <param name="documentInfoKeysToRemove">Specific document info keys to remove when not clearing the dictionary.</param>
    /// <param name="removeEmbeddedStandardFonts">Removes embedded font streams for the 14 standard fonts when set.</param>
    /// <param name="targetPdfVersion">Sets the compatibility level for the resulting PDF when specified.</param>
    /// <param name="mergeDuplicateFontSubsets">Merges duplicate font subset resources before applying other optimizations when set.</param>
    public PdfOptimizationSettings(
        int? compressionLevel = null,
        bool enableFullCompression = false,
        bool enableSmartMode = false,
        bool removeUnusedObjects = false,
        bool removeXmpMetadata = false,
        bool clearDocumentInfo = false,
        IEnumerable<string>? documentInfoKeysToRemove = null,
        bool removeEmbeddedStandardFonts = false,
        PdfCompatibilityLevel? targetPdfVersion = null,
        bool mergeDuplicateFontSubsets = false)
    {
        CompressionLevel = compressionLevel;
        EnableFullCompression = enableFullCompression;
        EnableSmartMode = enableSmartMode;
        RemoveUnusedObjects = removeUnusedObjects;
        RemoveXmpMetadata = removeXmpMetadata;
        ClearDocumentInfo = clearDocumentInfo;
        RemoveEmbeddedStandardFonts = removeEmbeddedStandardFonts;
        TargetPdfVersion = targetPdfVersion;
        MergeDuplicateFontSubsets = mergeDuplicateFontSubsets;

        if (clearDocumentInfo || documentInfoKeysToRemove is null)
        {
            DocumentInfoKeysToRemove = Array.Empty<string>();
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keys = new List<string>();
        foreach (var key in documentInfoKeysToRemove)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (seen.Add(key))
            {
                keys.Add(key);
            }
        }

        DocumentInfoKeysToRemove = new ReadOnlyCollection<string>(keys);
    }

    /// <summary>
    /// Gets the compression level applied to generated streams or <c>null</c> to keep defaults.
    /// </summary>
    public int? CompressionLevel { get; }

    /// <summary>
    /// Gets a value indicating whether the compact cross-reference compression mode is enabled.
    /// </summary>
    public bool EnableFullCompression { get; }

    /// <summary>
    /// Gets a value indicating whether iText smart mode should deduplicate resources.
    /// </summary>
    public bool EnableSmartMode { get; }

    /// <summary>
    /// Gets a value indicating whether unused objects should be removed before saving the PDF.
    /// </summary>
    public bool RemoveUnusedObjects { get; }

    /// <summary>
    /// Gets a value indicating whether XMP metadata should be removed from the catalog.
    /// </summary>
    public bool RemoveXmpMetadata { get; }

    /// <summary>
    /// Gets a value indicating whether the legacy document info dictionary should be cleared.
    /// </summary>
    public bool ClearDocumentInfo { get; }

    /// <summary>
    /// Gets the set of legacy document info keys that should be removed.
    /// </summary>
    public IReadOnlyCollection<string> DocumentInfoKeysToRemove { get; } = Array.Empty<string>();

    /// <summary>
    /// Gets a value indicating whether embedded standard font files should be removed.
    /// </summary>
    public bool RemoveEmbeddedStandardFonts { get; }

    /// <summary>
    /// Gets the desired compatibility level for the output PDF or <c>null</c> to keep the original version.
    /// </summary>
    public PdfCompatibilityLevel? TargetPdfVersion { get; }

    /// <summary>
    /// Gets a value indicating whether duplicate font subset resources should be merged before applying other optimizations.
    /// </summary>
    public bool MergeDuplicateFontSubsets { get; }

    /// <summary>
    /// Gets default optimization settings that keep the original document layout.
    /// </summary>
    public static PdfOptimizationSettings Default { get; } = new();
}
