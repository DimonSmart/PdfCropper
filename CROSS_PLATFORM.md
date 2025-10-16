# Cross-Platform Compatibility

## Supported Platforms

DimonSmart.PdfCropper is designed to work across multiple operating systems and .NET versions:

### Operating Systems
✅ **Windows** (x64, x86, ARM64)  
✅ **Linux** (x64, ARM64, including Ubuntu, Debian, CentOS, RHEL, Alpine)  
✅ **macOS** (x64, ARM64/Apple Silicon)  
✅ **Other Unix systems** (FreeBSD, etc., where .NET is supported)  

### .NET Versions
✅ **.NET 8.0** (LTS)  
✅ **.NET 9.0**  

## Platform-Specific Dependencies

The library automatically includes platform-specific native libraries:

### PDF Rendering (PDFium)
- **Windows**: `bblanchon.PDFium.Win32`
- **Linux**: `bblanchon.PDFium.Linux`
- **macOS**: `bblanchon.PDFium.macOS`

### Graphics Processing (SkiaSharp)
- **Windows**: `SkiaSharp.NativeAssets.Win32`
- **Linux**: `SkiaSharp.NativeAssets.Linux.NoDependencies`
- **macOS**: `SkiaSharp.NativeAssets.macOS`
- **WebAssembly**: `SkiaSharp.NativeAssets.WebAssembly` (for Blazor scenarios)

## Installation Requirements

### Linux Systems
For optimal PDF rendering on Linux, you may need to install additional packages:

```bash
# Ubuntu/Debian
sudo apt-get update
sudo apt-get install libfontconfig1 libgdiplus libc6-dev

# CentOS/RHEL/Fedora
sudo yum install fontconfig gdiplus-devel glibc-devel
# or with dnf
sudo dnf install fontconfig gdiplus-devel glibc-devel

# Alpine Linux
sudo apk add fontconfig libgdiplus
```

### macOS
No additional dependencies required. Works out of the box.

### Windows
No additional dependencies required. Works out of the box.

## Deployment Considerations

### Docker
When deploying in Docker containers, especially Alpine-based images, ensure you include the necessary runtime libraries:

```dockerfile
# For Alpine-based images
RUN apk add --no-cache fontconfig libgdiplus

# For Ubuntu-based images
RUN apt-get update && apt-get install -y libfontconfig1 libgdiplus && rm -rf /var/lib/apt/lists/*
```

### Self-Contained Deployments
The library works well with self-contained deployments. Native dependencies are automatically included based on the target runtime identifier (RID).

### Cloud Platforms
- **Azure App Service**: ✅ Supported
- **AWS Lambda**: ✅ Supported (with Amazon.Lambda.AspNetCoreServer)
- **Google Cloud Functions**: ✅ Supported
- **Azure Functions**: ✅ Supported

## Known Limitations

1. **ARM32**: Not supported due to PDFium library limitations
2. **WASM**: Limited support - BitmapBased method only
3. **iOS/Android**: Not tested, may require additional configuration

## Testing Cross-Platform Compatibility

The library has been tested on:
- Windows 10/11 (x64)
- Ubuntu 20.04/22.04 LTS (x64)
- macOS Big Sur+ (x64, ARM64)
- Docker containers (Alpine, Ubuntu)

## Performance Notes

Performance may vary slightly between platforms:
- **Windows**: Best performance, native PDFium integration
- **Linux**: Excellent performance, requires system libraries
- **macOS**: Excellent performance, native integration

## Troubleshooting

### Common Issues

**Linux: "Unable to load shared library 'libSkiaSharp'"**
```bash
sudo apt-get install libfontconfig1 libgdiplus
```

**macOS: "dyld: Library not loaded"**
- Ensure you're using a compatible .NET runtime version
- Try running with `sudo` if permission issues occur

**General: OutOfMemoryException on large PDFs**
- This is not platform-specific but may vary by available system memory
- Consider processing large PDFs in smaller chunks