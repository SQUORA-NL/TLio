FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY nuget.config /src/nuget.config
COPY TLio.Core/TLio.Core.csproj TLio.Core/
COPY TLio.Commands/TLio.Commands.csproj TLio.Commands/
COPY TLio.Functions/TLio.Functions.csproj TLio.Functions/
COPY TLio.Client/TLio.Client.csproj TLio.Client/
COPY TLio.Json/TLio.Json.csproj TLio.Json/
COPY samples/TLio.Sample.DockerPlugin/TLio.Sample.DockerPlugin.csproj samples/TLio.Sample.DockerPlugin/

RUN dotnet restore samples/TLio.Sample.DockerPlugin/TLio.Sample.DockerPlugin.csproj

COPY TLio.Core/ TLio.Core/
COPY TLio.Commands/ TLio.Commands/
COPY TLio.Functions/ TLio.Functions/
COPY TLio.Client/ TLio.Client/
COPY TLio.Json/ TLio.Json/
COPY samples/TLio.Sample.DockerPlugin/ samples/TLio.Sample.DockerPlugin/

RUN dotnet publish samples/TLio.Sample.DockerPlugin/TLio.Sample.DockerPlugin.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN mkdir -p /plugins

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV NUPLANE_PLUGINS_PATH=/plugins

EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "TLio.Sample.DockerPlugin.dll"]
