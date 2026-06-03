FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY opendatahub-static-generator-worker/opendatahub-static-generator-worker.csproj opendatahub-static-generator-worker/
RUN dotnet restore opendatahub-static-generator-worker/opendatahub-static-generator-worker.csproj
COPY opendatahub-static-generator-worker/ opendatahub-static-generator-worker/
RUN dotnet publish opendatahub-static-generator-worker/opendatahub-static-generator-worker.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "opendatahub-static-generator-worker.dll"]
