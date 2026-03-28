# API Contract: TLio.Sample.Api

**Version**: 1.0 | **Date**: 2026-03-28

## Endpoints

### POST /transform/json

Transform a JSON document using the bundled JSON script.

**Request**

| Item | Value |
|---|---|
| Method | POST |
| Path | `/transform/json` |
| Content-Type | `application/json` |
| Body | Raw JSON document to transform |

**Responses**

| Status | Content-Type | Body | Condition |
|---|---|---|---|
| 200 OK | `application/json` | Transformed JSON document | Script executed successfully |
| 400 Bad Request | `application/json` | `{ "error": "<message>" }` | Request body is empty or not valid JSON |
| 422 Unprocessable Entity | `application/json` | `{ "error": "<message>", "log": [...] }` | Script executed but reported failure |

---

### POST /transform/xml

Transform an XML document using the bundled XML script.

**Request**

| Item | Value |
|---|---|
| Method | POST |
| Path | `/transform/xml` |
| Content-Type | `application/xml` |
| Body | Raw XML document to transform |

**Responses**

| Status | Content-Type | Body | Condition |
|---|---|---|---|
| 200 OK | `application/xml` | Transformed XML document | Script executed successfully |
| 400 Bad Request | `application/json` | `{ "error": "<message>" }` | Request body is empty or not valid XML |
| 422 Unprocessable Entity | `application/json` | `{ "error": "<message>", "log": [...] }` | Script executed but reported failure |

---

### POST /transform/yaml

Transform a YAML document using the bundled YAML script.

**Request**

| Item | Value |
|---|---|
| Method | POST |
| Path | `/transform/yaml` |
| Content-Type | `text/yaml` |
| Body | Raw YAML document to transform |

**Responses**

| Status | Content-Type | Body | Condition |
|---|---|---|---|
| 200 OK | `text/yaml` | Transformed YAML document | Script executed successfully |
| 400 Bad Request | `application/json` | `{ "error": "<message>" }` | Request body is empty or not valid YAML |
| 422 Unprocessable Entity | `application/json` | `{ "error": "<message>", "log": [...] }` | Script executed but reported failure |

---

### Any other path / method

| Status | Body |
|---|---|
| 404 Not Found | (default ASP.NET Core response) |

## Error Response Shape

All error bodies follow the same JSON structure regardless of the endpoint format:

```json
{
  "error": "Human-readable error message",
  "log": [
    { "level": "Warning", "group": "command-execution", "message": "..." }
  ]
}
```

The `log` array is omitted on 400 responses (input was not parsed at all); it is included on 422 responses to surface TLio execution log entries.

## Default Port

The sample runs on `http://localhost:5100` by default when started with `dotnet run`.
