# Versioning

**The git tag is the version.** There is no version number written down anywhere in this
repository — not in `Directory.Build.props`, not in any `.csproj`, not in a workflow file.
[MinVer](https://github.com/adamralph/minver) reads the nearest `v*` tag at build time and
stamps it onto the assemblies and the packages. A tag and a NuGet version cannot drift apart,
because there is only one number.

That also means `dotnet pack` on your laptop produces the same version the pipeline does.

```sh
$ git tag                      # v0.8.0, three commits back
$ dotnet pack TLio.Core -c Release -o ./out
Successfully created package 'out/TLio.Core.0.9.0-preview.3.nupkg'
```

## What gets published, and when

| Trigger | Version | Example |
|---|---|---|
| Push / merge to `main` | next **minor** of the last release tag, plus the commit height | `0.9.0-preview.3` |
| Push a tag `vX.Y.Z` | exactly `X.Y.Z` | `v0.9.0` → `0.9.0` |
| Push a tag `vX.Y.Z-rc.1` | exactly `X.Y.Z-rc.1` | `v1.0.0-rc.1` → `1.0.0-rc.1` |

Every preview sorts **above** the release it follows and **below** the release it is heading
towards: `0.8.0` < `0.9.0-preview.3` < `0.9.0-preview.11` < `0.9.0`. All twelve packages always
carry the same number; the pipeline fails if they ever don't.

## Steering the next release

### Cutting a release — patch, minor or major

Run the **Release** workflow (Actions → Release → *Run workflow*) and pick the bump. It reads
the latest release tag, computes the next one, pushes it, publishes the packages, and creates
the GitHub release.

| From `v1.4.2`, choosing… | New tag | Use it for |
|---|---|---|
| `patch` | `v1.4.3` | bug fixes only, no API change |
| `minor` | `v1.5.0` | new commands, functions or adapters; backwards compatible |
| `major` | `v2.0.0` | a breaking change to script syntax or public API |
| `exact` | whatever you type | pre-releases (`1.5.0-rc.1`), or correcting course |

Tick **dry run** to see the tag it would create without creating it.

Doing it by hand is exactly equivalent — the workflow has no privileged knowledge:

```sh
git tag v1.5.0
git push origin v1.5.0
```

### Steering the *previews* on main

Untagged commits on `main` assume the next release is a **minor** one, because that is what
most merges here are. When the work in flight is something else, tell the repository by
tagging, not by editing a version number:

**Heading for a major release.** Push a pre-release tag once, and every preview after it is
based on that:

```sh
git tag v2.0.0-alpha.1
git push origin v2.0.0-alpha.1
# previews on main become 2.0.0-alpha.1.1, 2.0.0-alpha.1.2, …
# then release it for real with the Release workflow (bump = exact, 2.0.0)
```

**Heading for a patch release.** Same move with `v1.4.3-alpha.1`, or simply cut the patch
release when it is ready — a `patch` bump from `v1.4.2` gives `v1.4.3` regardless of what the
previews were called.

**Permanently.** If minor stops being the right default, change `MinVerAutoIncrement` in
`Directory.Build.props` to `patch` or `major`. This only affects the preview versions built
from untagged commits; release versions are always exactly their tag.

## AssemblyVersion is not the package version

Three numbers come out of one tag, and they are not the same number:

| Stamped as | From tag `v0.9.0` | Moves on |
|---|---|---|
| package version, `Version`, `InformationalVersion` | `0.9.0` | every release |
| `FileVersion` | `0.9.0.0` | every release |
| `AssemblyVersion` | `0.0.0.0` | major releases only |

`AssemblyVersion` is major-only, and that is load-bearing rather than incidental.
`samples/TLio.Sample.DockerPlugin` hot-loads extension packs as `.nupkg` files dropped into
`/plugins`, and the CLR binds those packs against the host's `TLio.Core` **by AssemblyVersion**.
Widen it to major.minor and a pack built against `0.9.0.0` stops loading into a `0.10.0` host —
every plugin in the wild breaks on every minor release:

```
warn: PluginLoader — Could not inspect assembly 'TLio.Extensions.Math':
      Could not load file or assembly 'TLio.Core, Version=0.9.0.0'
```

So: **do not derive `AssemblyVersion` from the minor.** Consumers still see exactly which build
they have — that is what the package version and `FileVersion` are for.

The same constraint is why the Docker sample takes a `TLIO_VERSION` build arg. Its build context
has no `.git`, so MinVer falls back to `0.0.0-nogit`; that happens to match any `0.x` plugin
today because the major agrees, but once TLio reaches `1.0.0` the image has to be built with the
real version or the packs stop binding.

## Rules the pipeline enforces

- A tag must be `vX.Y.Z` or `vX.Y.Z-prerelease`. `v0.9`, `0.9.0` and `release-1` are rejected
  before anything is built — an unparseable tag would otherwise be silently ignored by MinVer
  and publish a *preview* under what everyone believed was a release.
- `-preview.N` is reserved for the automatic previews. Hand-cut pre-releases use `-alpha`,
  `-beta` or `-rc`, so the two never interleave.
- The version inside the produced `.nupkg` files is checked against the tag before anything is
  pushed to NuGet.org. A shallow CI checkout — the classic way to break tag-based versioning —
  fails the build instead of publishing a wrong number.
- Re-running a publish is safe: `--skip-duplicate` means an already-published version is left
  alone.

## Consuming a version

```sh
dotnet add package TLio.Json                # latest stable
dotnet add package TLio.Json --prerelease   # latest preview from main
dotnet add package TLio.Json --version 0.9.0
```

## History

Versions `0.1.0-preview.3` … `0.1.0-preview.22` on NuGet.org predate this scheme. They came
from a hardcoded `0.1.0-preview.{run_number}` in the publish workflow, which kept being emitted
after `v0.8.0` was released — so those previews sort *below* the stable release rather than
above it. They are left in place; everything from `0.9.0-preview.*` onward follows the rules
above.
