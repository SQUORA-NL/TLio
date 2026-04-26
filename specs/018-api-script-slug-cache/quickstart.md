# Quickstart: Script Slug Cache

**Feature**: `018-api-script-slug-cache`  
**Date**: 2026-04-26

---

## 1. Start the API

```sh
dotnet run --project samples/TLio.Sample.Api
```

Or with Docker:

```sh
docker compose up --build
```

---

## 2. Register a script

```sh
curl -X POST http://localhost:5000/scripts \
  -H "Content-Type: application/json" \
  -d '{
    "slug": "add-greeting",
    "script": "[{\"command\":\"set\",\"path\":\"$.greeting\",\"value\":\"Hello\"}]"
  }'
```

Expected response (201 Created):
```json
{ "slug": "add-greeting", "status": "registered" }
```

---

## 3. Execute the script

Send any JSON document as the request body — it becomes the script input:

```sh
curl -X POST http://localhost:5000/run/add-greeting \
  -H "Content-Type: application/json" \
  -d '{"name": "World"}'
```

Expected response (200 OK):
```json
{ "name": "World", "greeting": "Hello" }
```

---

## 4. Execute with XML input

The same slug works with XML input automatically:

```sh
curl -X POST http://localhost:5000/run/add-greeting \
  -H "Content-Type: application/xml" \
  -d '<root><name>World</name></root>'
```

Expected response (200 OK, `application/xml`):
```xml
<root><name>World</name><greeting>Hello</greeting></root>
```

---

## 5. List registered scripts

```sh
curl http://localhost:5000/scripts
```

---

## 6. Delete a script

```sh
curl -X DELETE http://localhost:5000/scripts/add-greeting
```

Expected response: 204 No Content

---

## 7. Seed scripts at startup

Create or edit `scripts-config.json` next to the application binary:

```json
[
  {
    "slug": "stamp-processed",
    "script": "[{\"command\":\"set\",\"path\":\"$.processed\",\"value\":true}]"
  }
]
```

Or point to a custom path via environment variable:

```sh
TLIO_SCRIPTS_CONFIG=/data/my-scripts.json dotnet run --project samples/TLio.Sample.Api
```

Scripts in this file are available immediately after startup without any registration call.

---

## Error reference

| Scenario | Status | Message |
|----------|--------|---------|
| Unknown slug | 404 | `"Slug 'foo' is not registered"` |
| Script runtime error | 422 | `"Script execution failed: <detail>"` |
| Invalid slug characters | 400 | `"Invalid slug format"` |
| Unrecognisable request body | 400 | `"Cannot detect input format: ..."` |
| Script compilation failure | 400 | `"Script compilation failed: <detail>"` |
