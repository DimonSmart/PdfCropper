using System;
using System.Collections.Generic;

namespace DimonSmart.PdfCropper;

/// <summary>
/// Represents a reusable combination of cropping and optimization settings.
/// </summary>
public sealed class PdfCropProfile
{
    public PdfCropProfile(
        string key,
        string displayName,
        string description,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Profile key must be provided.", nameof(key));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name must be provided.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description must be provided.", nameof(description));
        }

        Key = key;
        DisplayName = displayName;
        Description = description;
        CropSettings = cropSettings;
        OptimizationSettings = optimizationSettings ?? throw new ArgumentNullException(nameof(optimizationSettings));
    }

    /// <summary>
    /// Gets the canonical identifier of the profile.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets a human-friendly name describing the profile.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets a short description of the intended usage scenario.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the cropping settings contained in the profile.
    /// </summary>
    public CropSettings CropSettings { get; }

    /// <summary>
    /// Gets the optimization settings contained in the profile.
    /// </summary>
    public PdfOptimizationSettings OptimizationSettings { get; }
}

/// <summary>
/// Provides predefined cropping profiles for common workflows.
/// </summary>
public static class PdfCropProfiles
{
    public const string SimpleKey = "simple";
    public const string EbookKey = "ebook";
    public const string AggressiveKey = "aggressive";

    private static readonly IReadOnlyList<PdfCropProfile> AllProfiles;
    private static readonly IReadOnlyDictionary<string, PdfCropProfile> ProfileMap;
    private static readonly IReadOnlyList<string> ProfileKeys;

    static PdfCropProfiles()
    {
        var simpleProfile = new PdfCropProfile(
            SimpleKey,
            "Simple crop",
            "Content-based cropping with the default margin and no additional optimizations.",
            CropSettings.Default,
            PdfOptimizationSettings.Default);

        var ebookProfile = new PdfCropProfile(
            EbookKey,
            "Edge-aware e-book crop",
            "Optimized for e-book readers: ignore edge-touching artefacts, exclude repeated content, and keep a comfortable 1pt margin.",
            new CropSettings(CropMethod.ContentBased, excludeEdgeTouchingObjects: true, margin: 1.0f, detectRepeatedObjects: true),
            PdfOptimizationSettings.Default);

        var aggressiveOptimization = new PdfOptimizationSettings(
            ResolveCompressionLevel(PdfCompressionLevels.BestCompression),
            enableFullCompression: true,
            enableSmartMode: true,
            removeUnusedObjects: true,
            removeXmpMetadata: true,
            clearDocumentInfo: true,
            documentInfoKeysToRemove: null,
            removeEmbeddedStandardFonts: true,
            targetPdfVersion: PdfCompatibilityLevel.Pdf17);

        var aggressiveProfile = new PdfCropProfile(
            AggressiveKey,
            "Aggressive crop + optimize",
            "Maximum size reduction: tight crop, exclude repeated content, all clean-up toggles on, and strongest compression.",
            new CropSettings(CropMethod.ContentBased, excludeEdgeTouchingObjects: true, margin: 0.25f, detectRepeatedObjects: true),
            aggressiveOptimization);

        var profiles = new[]
        {
            simpleProfile,
            ebookProfile,
            aggressiveProfile
        };

        var keys = new string[profiles.Length];
        var map = new Dictionary<string, PdfCropProfile>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < profiles.Length; i++)
        {
            var profile = profiles[i];
            keys[i] = profile.Key;
            map[profile.Key] = profile;
        }

        AllProfiles = Array.AsReadOnly(profiles);
        ProfileMap = new Dictionary<string, PdfCropProfile>(map);
        ProfileKeys = Array.AsReadOnly(keys);
    }

    /// <summary>
    /// Gets the collection of predefined profiles.
    /// </summary>
    public static IReadOnlyList<PdfCropProfile> All => AllProfiles;

    /// <summary>
    /// Gets the default content-based profile with minimal configuration.
    /// </summary>
    public static PdfCropProfile Simple => ProfileMap[SimpleKey];

    /// <summary>
    /// Gets the e-book-friendly profile that ignores edge artefacts.
    /// </summary>
    public static PdfCropProfile Ebook => ProfileMap[EbookKey];

    /// <summary>
    /// Gets the most aggressive profile with all optimisation toggles enabled.
    /// </summary>
    public static PdfCropProfile Aggressive => ProfileMap[AggressiveKey];

    /// <summary>
    /// Gets the set of keys that can be used to resolve a profile.
    /// </summary>
    public static IReadOnlyList<string> Keys => ProfileKeys;

    /// <summary>
    /// Attempts to resolve a profile by its key.
    /// </summary>
    public static bool TryGet(string key, out PdfCropProfile profile)
    {
        return ProfileMap.TryGetValue(key, out profile!);
    }

    private static int ResolveCompressionLevel(string name)
    {
        if (PdfCompressionLevels.TryGetValue(name, out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Compression level '{name}' is not supported.");
    }
}
