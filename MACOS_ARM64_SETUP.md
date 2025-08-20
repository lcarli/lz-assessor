# Azure Functions Development on macOS ARM64

This document provides specific guidance for developing Azure Functions on Apple Silicon Macs.

## Issue Description

When running `func start` on macOS with Apple Silicon (ARM64), you may encounter:

```
error : Metadata generation failed. Exit code: '130' Error: 'Failed to load /usr/local/share/dotnet/shared/Microsoft.NETCore.App/3.1.19/libhostpolicy.dylib, error: dlopen(..., 0x0001): tried: '...' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64e' or 'arm64'))'
```

## Root Cause

This error occurs when:
1. Azure Functions Core Tools tries to load x86_64 .NET libraries on ARM64 architecture
2. Multiple .NET versions are installed with architecture conflicts
3. The Azure Functions runtime defaults to an incompatible architecture

## Solution Steps

### 1. Install Correct Azure Functions Core Tools

```bash
# Remove any existing installation
npm uninstall -g azure-functions-core-tools
brew uninstall azure-functions-core-tools

# Install ARM64-compatible version
brew install azure/functions/azure-functions-core-tools@4
```

### 2. Verify .NET 8 ARM64 Installation

```bash
# Check .NET version and architecture
dotnet --info
dotnet --version

# Should show .NET 8.0.x and ARM64 architecture
# If not, download .NET 8 ARM64 from https://dotnet.microsoft.com/download
```

### 3. Clear .NET Cache

```bash
# Clear NuGet packages cache
dotnet nuget locals all --clear

# Clear MSBuild cache
dotnet clean
dotnet restore
```

### 4. Force ARM64 Runtime

Option A - Environment variable:
```bash
export DOTNET_CLI_ARCHITECTURE=arm64
export DOTNET_ROOT=/usr/local/share/dotnet
func start
```

Option B - Explicit runtime:
```bash
dotnet run --runtime osx-arm64
```

Option C - Force architecture:
```bash
arch -arm64 func start
```

### 5. Alternative: Use Docker

If issues persist, use Docker for development:

```bash
# Create Dockerfile for development
docker run -it --rm \
  -v $(pwd):/workspace \
  -w /workspace \
  -p 7071:7071 \
  mcr.microsoft.com/azure-functions/dotnet-isolated:8-appservice \
  func start --host 0.0.0.0
```

## Project Configuration

The project includes these compatibility fixes:

1. **global.json** - Specifies .NET 8 SDK requirement
2. **Project file** - Includes ARM64 runtime detection
3. **local.settings.json** - Proper isolated worker configuration
4. **.funcignore** - Excludes unnecessary files from processing

## Verification

After applying the solution:

```bash
cd src/LzAssessor.NewVersion
func start
```

You should see:
```
Azure Functions Core Tools
Core Tools Version: 4.x.x
Function Runtime Version: 4.x.x

[2024-xx-xx] Host started (1234ms)
[2024-xx-xx] Job host started
```

## Additional Resources

- [Azure Functions Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [.NET 8 Downloads](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions on Apple Silicon](https://github.com/Azure/azure-functions-core-tools/issues/2834)