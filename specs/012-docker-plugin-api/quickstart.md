# Quickstart: Docker Plugin API Sample

## Prerequisites

- Docker Desktop installed and running
- (Optional) .NET 10 SDK for local development

---

## Scenario 1: Start with no plugins — verify baseline

```bash
# Start the container with an empty plugins folder
mkdir plugins
docker compose up -d

# Call a Math function (not loaded yet) — expect failure
curl -X POST http://localhost:5000/transform/json \
  -H "Content-Type: application/json" \
  -d '{"input":{"value":3.7},"script":[{"command":"set","path":"$.rounded","value":"=round($.value)"}]}'

# Expected: { "success": false, "error": "Unknown function: round" }

# Check loaded plugins — empty
curl http://localhost:5000/plugins
# Expected: { "plugins": [], "builtinFunctions": [...] }
```

---

## Scenario 2: Drop a plugin — verify it activates

```bash
# Copy the Math extension package into the watched folder
cp path/to/TLio.Extensions.Math.0.1.0.nupkg ./plugins/

# Wait up to 10 seconds for NuPlane to detect and load the package
sleep 5

# Check plugins endpoint — should now show Math
curl http://localhost:5000/plugins

# Retry the Math function — expect success
curl -X POST http://localhost:5000/transform/json \
  -H "Content-Type: application/json" \
  -d '{"input":{"value":3.7},"script":[{"command":"set","path":"$.rounded","value":"=round($.value)"}]}'
# Expected: { "success": true, "data": { "value": 3.7, "rounded": 4 } }
```

---

## Scenario 3: Remove the plugin — verify functions stop working

```bash
# Remove the package from the plugins folder
rm ./plugins/TLio.Extensions.Math.0.1.0.nupkg

# Wait for NuPlane to detect the removal
sleep 5

# Math function should no longer work
curl -X POST http://localhost:5000/transform/json \
  -H "Content-Type: application/json" \
  -d '{"input":{"value":3.7},"script":[{"command":"set","path":"$.rounded","value":"=round($.value)"}]}'
# Expected: { "success": false, "error": "Unknown function: round" }

# Plugins endpoint is empty again
curl http://localhost:5000/plugins
# Expected: { "plugins": [], "builtinFunctions": [...] }
```

---

## Scenario 4: Load multiple plugins simultaneously

```bash
cp path/to/TLio.Extensions.Math.0.1.0.nupkg ./plugins/
cp path/to/TLio.Extensions.Text.0.1.0.nupkg ./plugins/
sleep 10

# Both extensions available
curl http://localhost:5000/plugins
# Expected: plugins array contains both Math and Text entries

# Use functions from both
curl -X POST http://localhost:5000/transform/json \
  -H "Content-Type: application/json" \
  -d '{"input":{"price":9.999,"label":"hello"},"script":[{"command":"set","path":"$.price","value":"=round($.price)"},{"command":"set","path":"$.label","value":"=toUpper($.label)"}]}'
# Expected: { "success": true, "data": { "price": 10, "label": "HELLO" } }
```

---

## Scenario 5: Invalid file — no crash

```bash
echo "not a nuget package" > ./plugins/garbage.nupkg
sleep 5

# Container still running
curl http://localhost:5000/health
# Expected: { "status": "healthy" }

# Error appears in logs but no crash
docker logs tlio-plugin-api 2>&1 | grep -i "warn\|error"
```

---

## docker-compose.yml reference

```yaml
services:
  tlio-plugin-api:
    image: tlio-sample-docker-plugin:latest
    ports:
      - "5000:8080"
    volumes:
      - ./plugins:/plugins
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
```

## Build locally

```bash
cd samples/TLio.Sample.DockerPlugin
docker build -t tlio-sample-docker-plugin .
docker compose up
```
