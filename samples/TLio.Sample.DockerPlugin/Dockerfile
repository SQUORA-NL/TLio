FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The version the TLio assemblies inside the image are stamped with. The build context has no
# .git, so MinVer cannot derive it here (see Directory.Build.props) and falls back to
# 0.0.0-nogit. Pass the real one to keep the host and the plugin packs people drop into
# /plugins on the same AssemblyVersion:
#
#     docker build --build-arg TLIO_VERSION=$(dotnet msbuild TLio.Core/TLio.Core.csproj \
#         -t:MinVer -getProperty:Version | jq -r .Properties.Version) ...
#
# Only the major has to agree — AssemblyVersion is major-only on purpose, so any 0.x host
# accepts any 0.x pack, which is why the default below is fine for local runs.
ARG TLIO_VERSION=0.0.0-nogit

# Both COPY lists below must hold every ProjectReference of the sample. TLio.Xml and TLio.Yaml
# were missing here after the sample gained XML and YAML endpoints: restore does not object to
# a ProjectReference it cannot find, so the gap only surfaced as CS0246 during publish.
COPY nuget.config /src/nuget.config
COPY Directory.Build.props /src/Directory.Build.props
COPY Directory.Build.targets /src/Directory.Build.targets
# Every assembly is strong-named, and the plugin host's SharedAssembly() policy is keyed on the
# resulting public key token — so the key has to be here or the packs stop binding to the host.
COPY tlio.snk /src/tlio.snk
COPY TLio.Core/TLio.Core.csproj TLio.Core/
COPY TLio.Commands/TLio.Commands.csproj TLio.Commands/
COPY TLio.Functions/TLio.Functions.csproj TLio.Functions/
COPY TLio.Client/TLio.Client.csproj TLio.Client/
COPY TLio.Json/TLio.Json.csproj TLio.Json/
COPY TLio.Xml/TLio.Xml.csproj TLio.Xml/
COPY TLio.Yaml/TLio.Yaml.csproj TLio.Yaml/
COPY samples/TLio.Sample.DockerPlugin/TLio.Sample.DockerPlugin.csproj samples/TLio.Sample.DockerPlugin/

RUN dotnet restore samples/TLio.Sample.DockerPlugin/TLio.Sample.DockerPlugin.csproj

COPY TLio.Core/ TLio.Core/
COPY TLio.Commands/ TLio.Commands/
COPY TLio.Functions/ TLio.Functions/
COPY TLio.Client/ TLio.Client/
COPY TLio.Json/ TLio.Json/
COPY TLio.Xml/ TLio.Xml/
COPY TLio.Yaml/ TLio.Yaml/
COPY samples/TLio.Sample.DockerPlugin/ samples/TLio.Sample.DockerPlugin/

RUN dotnet publish samples/TLio.Sample.DockerPlugin/TLio.Sample.DockerPlugin.csproj \
    -c Release -o /app/publish --no-restore -p:Version=$TLIO_VERSION

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is not in the aspnet runtime image, and both the HEALTHCHECK below and the one in
# docker-compose.yml call it — without this the container reported "unhealthy" for its whole
# life while the API answered perfectly well from outside.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /plugins

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV NUPLANE_PLUGINS_PATH=/plugins

EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "TLio.Sample.DockerPlugin.dll"]
