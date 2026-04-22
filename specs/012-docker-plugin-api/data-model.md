# Data Model: Docker-Based Plugin API with NuGet Hot-Loading

## Entities

### PluginPackage

Represents a `.nupkg` file placed in the plugins folder.

| Field | Type | Description |
|-------|------|-------------|
| `PackageId` | string | NuGet package identifier (e.g., `TLio.Extensions.Math`) |
| `Version` | string | SemVer string (e.g., `0.1.0`) |
| `FilePath` | string | Absolute path to the `.nupkg` file on disk |
| `Status` | PluginStatus | Current lifecycle state |
| `LoadedAt` | DateTimeOffset? | When the package was successfully loaded |
| `UnloadedAt` | DateTimeOffset? | When the package was successfully unloaded |
| `ErrorMessage` | string? | Populated if Status = Failed |

**Status transitions**:
```
Detected → Loading → Loaded → Unloading → Unloaded
                   ↘ Failed
```

---

### LoadedExtension

Represents a successfully loaded TLio extension from a plugin package.

| Field | Type | Description |
|-------|------|-------------|
| `PackageId` | string | Source package identifier |
| `AssemblyName` | string | CLR assembly name extracted from the package |
| `Functions` | string[] | Names of TLio functions contributed by this extension |
| `LoadContextId` | Guid | Identifier for the collectible `AssemblyLoadContext` |

---

### PluginCatalog

The runtime registry of all currently loaded plugins. Singleton in DI.

| Field | Type | Description |
|-------|------|-------------|
| `Entries` | IReadOnlyList\<LoadedExtension\> | All currently active extensions |
| `LastUpdated` | DateTimeOffset | When the catalog was last modified |

---

### PluginStatus (enum)

```
Detected   — file appeared in the folder; loading not yet started
Loading    — NuPlane is extracting and loading the package
Loaded     — assembly in memory; functions registered with TLio engine
Failed     — loading attempt failed; see ErrorMessage
Unloading  — file removed; unload in progress
Unloaded   — assembly collected; functions deregistered
```

---

## State Transitions

### Add plugin flow
```
1. .nupkg file written to /plugins
2. Nuplane.Sources.Directory detects file (FileSystemWatcher)
3. Nuplane reconciles desired state → emits package-loaded intent
4. Nuplane.Loading extracts DLLs into collectible AssemblyLoadContext
5. App receives IPackageLoadedEvent
6. App discovers IFunctionProvider<JToken> implementations via reflection
7. App registers them in MutableFunctionsProvider singleton
8. Plugin status → Loaded; PluginCatalog updated
```

### Remove plugin flow
```
1. .nupkg file deleted from /plugins
2. Nuplane detects removal → emits package-unloaded intent
3. App receives IPackageUnloadedEvent
4. App deregisters the package's IFunctionProvider<JToken> instances
5. AssemblyLoadContext marked for GC collection
6. Plugin status → Unloaded; PluginCatalog updated
```

---

## Plugin Assembly Contract

TLio extension packages that participate in hot-loading MUST:

1. Reference `TLio.Core` (or `TLio.Client`) via `PackageReference`
2. Contain at least one public class implementing `IFunctionProvider<TNode>` (where `TNode` is `JToken` for the JSON-backed sample)
3. Have no static initializers that hold unmanaged resources (required for safe unload from a collectible context)

Example conforming assembly: `TLio.Extensions.Math.dll` (already exists in TLio NuGet packages).
