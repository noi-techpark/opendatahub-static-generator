FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY opendatahub-static-api/opendatahub-static-api.csproj opendatahub-static-api/
RUN dotnet restore opendatahub-static-api/opendatahub-static-api.csproj
COPY opendatahub-static-api/ opendatahub-static-api/
RUN dotnet publish opendatahub-static-api/opendatahub-static-api.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "opendatahub-static-api.dll"]
