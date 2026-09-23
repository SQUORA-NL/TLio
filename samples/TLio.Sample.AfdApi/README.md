# TLio.Sample.AfdApi

A minimal demo API for converting SIVI AFD messages between AFD 1.0, AFD Short and AFD 2.0,
built entirely on this repo's own engine — no external service, no other repo required to
build or run it.

The three scripts under `Scripts/` and the sample messages under `SampleInput/` are a bundled
snapshot from the separate **SQUORA/TLIO-Afd** repo, which is where they're actually generated
and maintained (`tools/generate_afd_scripts.py`, driven by SIVI's own catalogue exports — see
that repo's README for the full mapping story, the round-trip test, and how to regenerate them
from a newer AOS export). This project is a consumer of that output, not the source of truth:
if the AFD catalogue changes, regenerate the scripts there and copy the three
`*.tlio.json` files (and any sample messages you want to demo) back into `Scripts/`/`SampleInput/`
here.

## Run it

```bash
cd samples/TLio.Sample.AfdApi
dotnet run
```

```bash
curl -s -X POST http://localhost:61514/convert/afd1-to-afd2 \
  -H "Content-Type: application/xml" --data-binary @SampleInput/afd1-sample.xml | python3 -m json.tool
```

`GET /` lists the three directions and every bundled sample file; `GET /samples/{name}` serves
one directly. `afd1-to-afd2` and `afd1-to-afdshort` take AFD 1.0 XML; `afdshort-to-afd2` takes
AFD Short JSON. All three return the converted document as JSON: HTTP 200 on success, HTTP 422
when the *engine itself* reports failure (a malformed script or unreadable input). An unmapped
attribute, an unknown entity, or a value that fails its type conversion is never a failure here —
it shows up as HTTP 200 with `_afdIssues` entries and, where relevant, `_afdQuarantine` content
in the response body.

`SampleInput/afd1-sample.xml` has five deliberate anomalies baked in specifically to exercise
that reporting; `SampleInput/afd1-clean-sample.xml` and `SampleInput/afdshort-clean-sample.json`
are the issue-free counterparts, for when you want to see the mapping with nothing to explain
away. See `TLIO-Afd/samples/TLio-AFD-Demo.postman_collection.json` (in the sibling repo) for a
Postman collection exercising both, plus the round-trip invariant
(`afd1 → afdShort → afd2` == `afd1 → afd2`) and basic error handling.

## Why this is a plain `<ProjectReference>`, unlike the AFD repo's own attempt at this

An earlier version of this demo lived inside the TLIO-Afd repo itself and referenced TLio via a
`<ProjectReference>` pointing at a git worktree path outside that repo — which meant TLIO-Afd
alone wasn't buildable, only ever a symptom of the underlying problem: the AFD scripts depend on
engine changes (resolve's indexed join and dynamic `targetPath`, `decisionTable`'s
evaluate-before-ensure ordering, the per-node `@.` resolution fix, and a large-array performance
fix) that weren't part of any TLio commit yet. Living here, in this repo, as a normal sibling
project under `samples/`, that problem doesn't exist — the reference is just `..\..\TLio.Client\
TLio.Client.csproj`, like every other sample in this folder.

## What it does not do

No auth, no request size limits beyond ASP.NET Core's defaults, no script caching (each request
re-parses the script text from disk — fine for a demo against messages a few KB in size; the
scripts here are several MB, so this is the dominant cost per request; a production deployment
would compile each script once with `ScriptEngine.Compile` and reuse the `CompiledScript`, the
way `TLio.Sample.Api`'s `ScriptRegistry` does). No try/catch around the conversion call either:
a genuinely unparseable input (not empty, but not valid XML/JSON) currently surfaces as a bare
HTTP 500 rather than the documented HTTP 422 — a known, small gap, not a design decision.
