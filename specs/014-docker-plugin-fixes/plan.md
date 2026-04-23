# Implementation Plan: Fix Docker Build — Missing Directory.Build.props

**Branch**: `main` | **Date**: 2026-04-22 | **Spec**: n/a (bug fix)  
**Input**: Docker build error — NETSDK1013 TargetFramework '' not recognised

## Summary

The Dockerfile for `TLio.Sample.DockerPlugin` copies individual `.csproj` files and then runs `dotnet restore`, but never copies `Directory.Build.props`. Because `TargetFramework` is defined only in `Directory.Build.props` (not in any `.csproj`), MSBuild sees an empty value and fails with NETSDK1013. The fix is a one-line addition: copy `Directory.Build.props` before the restore step.

## Technical Context

**Language/Version**: C# / .NET 10  
**Primary Dependencies**: Docker, MSBuild SDK, `Directory.Build.props` (global MSBuild properties)  
**Storage**: N/A  
**Testing**: Rebuild Docker image locally  
**Target Platform**: Linux container (mcr.microsoft.com/dotnet/sdk:10.0)  
**Project Type**: Docker build pipeline  
**Performance Goals**: N/A  
**Constraints**: Must not break layer-cache ordering (restore layer must still be separate from source copy layer)  
**Scale/Scope**: Single file change (Dockerfile)

## Root Cause

```
Directory.Build.props          ← defines <TargetFramework>net10.0</TargetFramework>
  └─ TLio.Core.csproj          ← no <TargetFramework> (inherits from props)
  └─ TLio.Sample.DockerPlugin.csproj  ← same
```

In the Dockerfile, steps 4–11 copy only `.csproj` files. `Directory.Build.props` is never copied. When step 12 runs `dotnet restore`, MSBuild resolves `<TargetFramework>` as empty → NETSDK1013.

## Constitution Check

| Gate | Article | Question | Answer |
|---|---|---|---|
| Format Neutrality | I | Format-specific types in Core/Commands/Functions? | No — Dockerfile change only |
| Dependency Inversion | II | New adapters or fetchers? | No |
| Generic-First | III | New public APIs? | No |
| Process/Execution separation | IV | Node reads/mutations outside adapters? | No |
| Swappable Selection | V | Hard-coded paths in commands/functions? | No |
| Test-First + Fixture Triplets | VI | New commands or functions? | No — no test changes required |
| Simplicity Gate | VII | Simpler option available? | Fix is the simplest possible (1 line) |
| Backward Migration Path | VIII | JLio behaviour changed? | No |
| No Leaking Internals | IX | Core public APIs affected? | No |
| Logging as Observability | X | Execute() paths affected? | No |
| AI Component Reference | XI | New command/function/adapter? | No |

All gates pass. No violations.

## Project Structure

No new files or projects. Only one file changes:

```text
samples/TLio.Sample.DockerPlugin/
└── Dockerfile    ← add COPY Directory.Build.props line (line 4, after nuget.config COPY)
```

## Fix

Add the following line to `samples/TLio.Sample.DockerPlugin/Dockerfile` immediately after the `nuget.config` COPY and before the first `.csproj` COPY:

```dockerfile
COPY Directory.Build.props /src/Directory.Build.props
```

**Before (line 4):**
```dockerfile
COPY nuget.config /src/nuget.config
COPY TLio.Core/TLio.Core.csproj TLio.Core/
...
```

**After:**
```dockerfile
COPY nuget.config /src/nuget.config
COPY Directory.Build.props /src/Directory.Build.props
COPY TLio.Core/TLio.Core.csproj TLio.Core/
...
```

This preserves the restore-layer cache optimisation: `Directory.Build.props` only invalidates the restore cache when global properties change, which is rare.

## Complexity Tracking

No violations to justify.
