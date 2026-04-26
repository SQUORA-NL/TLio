# HTTP API Contract: Script Slug Cache

**Feature**: `018-api-script-slug-cache`  
**Date**: 2026-04-26  
**Base path**: All endpoints relative to server root  
**Applies to**: `TLio.Sample.Api` and `TLio.Sample.DockerPlugin`

---

## Script Execution

### POST /run/{slug}

Execute the pre-compiled script registered under `{slug}`, using the request body as input data.

**Path parameters**

| Parameter | Type | Pattern | Description |
|-----------|------|---------|-------------|
| `slug` | string | `[a-z0-9\-_]+` | Registered script identifier |

**Request**

| Property | Value |
|----------|-------|
| Content-Type | Any — `application/json`, `application/xml`, `application/yaml`, or auto-detected |
| Body | Raw data document; becomes the script input |

**Responses**

| Status | Content-Type | Body | Condition |
|--------|-------------|------|-----------|
| 200 OK | Matches script output format | Serialised execution result | Script executed successfully |
| 400 Bad Request | `application/json` | `{ "error": "Cannot detect input format: ..." }` | Body format unrecognisable |
| 404 Not Found | `application/json` | `{ "error": "Slug '{slug}' is not registered" }` | Slug not in registry |
| 413 Payload Too Large | `application/json` | `{ "error": "Request body exceeds maximum size" }` | Body exceeds configured limit |
| 422 Unprocessable Entity | `application/json` | `{ "error": "Script execution failed: ..." }` | Script runtime error |

**Slug validation**: If `{slug}` contains characters outside `[a-z0-9\-_]`, returns 400 before registry lookup.

---

## Script Registry Management

### POST /scripts

Register or replace a script in the registry. The script is compiled for all supported formats immediately.

**Request**

| Property | Value |
|----------|-------|
| Content-Type | Any — JSON, XML, or YAML payload accepted |
| Body | Registration payload containing `slug` and `script` fields |

**JSON example**:
```json
{
  "slug": "add-greeting",
  "script": "[{\"command\":\"set\",\"path\":\"$.greeting\",\"value\":\"Hello\"}]"
}
```

**XML example**:
```xml
<registration>
  <slug>add-greeting</slug>
  <script>[{"command":"set","path":"$.greeting","value":"Hello"}]</script>
</registration>
```

**Responses**

| Status | Body | Condition |
|--------|------|-----------|
| 201 Created | `{ "slug": "add-greeting", "status": "registered" }` | New slug registered |
| 200 OK | `{ "slug": "add-greeting", "status": "replaced" }` | Existing slug replaced |
| 400 Bad Request | `{ "error": "Cannot parse registration payload: ..." }` | Payload unrecognisable or missing fields |
| 400 Bad Request | `{ "error": "Script compilation failed: ..." }` | Script contains invalid commands |

---

### GET /scripts

List all currently registered slugs.

**Request**: No body required.

**Response**: 200 OK, `application/json`

```json
[
  {
    "slug": "add-greeting",
    "source": "[{\"command\":\"set\",\"path\":\"$.greeting\",\"value\":\"Hello\"}]",
    "registeredAt": "2026-04-26T10:00:00Z"
  }
]
```

Returns an empty array `[]` when no slugs are registered.

---

### DELETE /scripts/{slug}

Remove a slug from the registry.

**Path parameters**: `slug` — same pattern as execution endpoint.

**Responses**

| Status | Condition |
|--------|-----------|
| 204 No Content | Slug removed successfully |
| 404 Not Found | Slug was not registered |

---

## Startup Configuration

Scripts seeded via `scripts-config.json` (or `TLIO_SCRIPTS_CONFIG` env var path) are loaded at startup. They follow the same compilation and validation rules as runtime-registered scripts. Invalid entries are logged and skipped; valid entries are always loaded.

**Config file schema** (JSON array):
```json
[
  {
    "slug": "startup-script",
    "script": "[{\"command\":\"set\",\"path\":\"$.processed\",\"value\":true}]"
  }
]
```

---

## Error Response Shape

All error responses use `application/json` with a single `error` string field:

```json
{ "error": "<human-readable description>" }
```

---

## Logging Contract

| Event | Log Level | Fields logged |
|-------|-----------|--------------|
| Slug registered (new) | Info | `slug`, `timestamp` |
| Slug replaced | Info | `slug`, `timestamp` |
| Slug deleted | Info | `slug`, `timestamp` |
| Execution: slug not found (404) | Warning | `slug` |
| Execution: runtime error (422) | Error | `slug`, `error message` |
| Execution: format undetectable (400) | Warning | `slug`, `content-type` |
| Registration: parse failure (400) | Warning | `slug` (if available), `error message` |
| Registration: compilation failure (400) | Warning | `slug`, `error message` |
| Startup config: invalid entry skipped | Warning | `slug`, `error message` |
| Execution: success (200) | _silent_ | — |
