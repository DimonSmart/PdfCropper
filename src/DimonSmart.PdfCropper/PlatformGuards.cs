using System.Runtime.Versioning;

namespace DimonSmart.PdfCropper;

internal static class PlatformGuards
{
    [SupportedOSPlatformGuard("windows")]
    public static bool IsWindows => OperatingSystem.IsWindows();

    [SupportedOSPlatformGuard("linux")]
    public static bool IsLinux => OperatingSystem.IsLinux();

    [SupportedOSPlatformGuard("macos")]
    public static bool IsMacOS => OperatingSystem.IsMacOS();

    [SupportedOSPlatformGuard("android31.0")]
    public static bool IsAndroid31Plus => OperatingSystem.IsAndroidVersionAtLeast(31);

    [SupportedOSPlatformGuard("ios13.6")]
    public static bool IsIOS136Plus => OperatingSystem.IsIOSVersionAtLeast(13, 6);

    [SupportedOSPlatformGuard("maccatalyst13.5")]
    public static bool IsMacCatalyst135Plus => OperatingSystem.IsMacCatalystVersionAtLeast(13, 5);
}
