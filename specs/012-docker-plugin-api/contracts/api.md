# API Contract: TLio Docker Plugin Sample

Base URL: `http://localhost:5000` (configurable via `ASPNETCORE_URLS`)

---

## POST /transform/{format}

Execute a TLio transformation script against a document.

**Path parameters**:
- `format`: `json` | `xml` | `yaml`

**Request body** (application/json):
```json
{
  "input": { },
  "script": [
    { "command": "set", "path": "$.field", "value": "=someFunction($.other)" }
  ]
}
```

**Response 200** (application/json):
```json
{
  "success": true,
  "data": { }
}
```

**Response 200 — function unavailable** (plugin not loaded):
```json
{
  "success": false,
  "error": "Unknown function: someFunction"
}
```

**Response 400** — malformed request body.

---

## GET /plugins

Returns the list of currently loaded plugin packages and their contributed functions.

**Response 200** (application/json):
```json
{
  "lastUpdated": "2026-04-21T10:30:00Z",
  "plugins": [
    {
      "packageId": "TLio.Extensions.Math",
      "version": "0.1.0",
      "loadedAt": "2026-04-21T10:30:00Z",
      "functions": ["round", "ceil", "floor", "abs", "min", "max"]
    }
  ],
  "builtinFunctions": ["concat", "fetch", "indirect", "scriptpath", "newGuid"]
}
```

**Response 200 — no plugins loaded**:
```json
{
  "lastUpdated": "2026-04-21T10:00:00Z",
  "plugins": [],
  "builtinFunctions": ["concat", "fetch", "indirect", "scriptpath", "newGuid"]
}
```

---

## GET /plugins/status

Returns the full reconciliation state including packages in transition.

**Response 200** (application/json):
```json
{
  "entries": [
    {
      "packageId": "TLio.Extensions.Math",
      "version": "0.1.0",
      "status": "Loaded",
      "loadedAt": "2026-04-21T10:30:00Z",
      "errorMessage": null
    },
    {
      "packageId": "TLio.Extensions.Text",
      "version": "0.1.0-preview.5",
      "status": "Failed",
      "loadedAt": null,
      "errorMessage": "Assembly TLio.Extensions.Text.dll not found in package"
    }
  ]
}
```

---

## GET /health

Liveness probe for Docker health check.

**Response 200**:
```json
{ "status": "healthy" }
```

---

## Notes

- The `POST /transform/{format}` endpoint is intentionally identical in shape to `TLio.Sample.Api` to allow direct comparison.
- The `/plugins` and `/plugins/status` endpoints are served by `Nuplane.Loading.Api` endpoint extensions registered in `Program.cs`.
- All responses use `application/json` regardless of the transform format; the format parameter only affects how TLio parses the input document.
