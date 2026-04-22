# TLio.Sample.DockerPlugin

A Docker-hosted TLio transformation API that hot-loads TLio extension NuGet packages at runtime. Drop a `.nupkg` into a volume-mounted folder — the API gains new functions within 10 seconds. Remove it — the functions are gone. No restart required.

Powered by [NuPlane](https://www.nuget.org/packages/Nuplane) for folder watching and assembly loading, and [CShells](https://www.nuget.org/packages/CShells) for the modular feature host.

## Prerequisites

- Docker Desktop

## Build and run

```bash
cd samples/TLio.Sample.DockerPlugin
mkdir -p plugins
docker build -t tlio-sample-docker-plugin -f Dockerfile ../..
docker compose up -d
```

## Endpoints

| Endpoint | Description |
|---|---|
| `POST /transform/{format}` | Execute a TLio script (`format`: `json`, `xml`, `yaml`) |
| `GET /plugins` | List currently loaded extensions and built-in functions |
| `GET /plugins/status` | Full NuPlane load-state catalog |
| `GET /health` | Liveness probe |

## Scenario 1: Baseline (no plugins)

```bash
curl -X POST http://localhost:5000/transform/json \
  -H "Content-Type: application/json" \
  -d '{"input":{"value":3.7},"script":[{"command":"set","path":"$.rounded","value":"=round($.value)"}]}'
# → { "success": false, "error": "Unknown function: round" }

curl http://localhost:5000/plugins
# → { "plugins": [], "builtinFunctions": [...] }
```

## Scenario 2: Add a plugin

```bash
cp path/to/TLio.Extensions.Math.0.1.0.nupkg ./plugins/
sleep 10

curl http://localhost:5000/plugins
# → plugins array now includes TLio.Extensions.Math

curl -X POST http://localhost:5000/transform/json \
  -H "Content-Type: application/json" \
  -d '{"input":{"value":3.7},"script":[{"command":"set","path":"$.rounded","value":"=round($.value)"}]}'
# → { "success": true, "data": { "value": 3.7, "rounded": 4 } }
```

## Scenario 3: Remove the plugin

```bash
rm ./plugins/TLio.Extensions.Math.0.1.0.nupkg
sleep 10

curl -X POST http://localhost:5000/transform/json \
  -H "Content-Type: application/json" \
  -d '{"input":{"value":3.7},"script":[{"command":"set","path":"$.rounded","value":"=round($.value)"}]}'
# → { "success": false, "error": "Unknown function: round" }
```

## Scenario 4: Multiple plugins

```bash
cp path/to/TLio.Extensions.Math.0.1.0.nupkg ./plugins/
cp path/to/TLio.Extensions.Text.0.1.0.nupkg ./plugins/
sleep 10

curl -X POST http://localhost:5000/transform/json \
  -H "Content-Type: application/json" \
  -d '{"input":{"price":9.999,"label":"hello"},"script":[{"command":"set","path":"$.price","value":"=round($.price)"},{"command":"set","path":"$.label","value":"=toUpper($.label)"}]}'
# → { "success": true, "data": { "price": 10, "label": "HELLO" } }
```

## Scenario 5: Invalid file — no crash

```bash
echo "not a nuget package" > ./plugins/garbage.nupkg
sleep 5

curl http://localhost:5000/health
# → { "status": "healthy" }

docker logs tlio-plugin-api 2>&1 | grep -i "warn"
# → warning about unrecognised package, no crash
```

## Conflict resolution

When two plugin packages register a function with the same name, the **last-registered** package's implementation is used. A warning is logged. To use the earlier package's version, remove the later package first.

## Stopping

```bash
docker compose down
```
