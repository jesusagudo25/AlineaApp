FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS source
WORKDIR /src
COPY global.json ./
COPY backend/ backend/
COPY tests/ tests/

FROM source AS tests
RUN dotnet restore tests/Alinea.Api.Tests/Alinea.Api.Tests.csproj --locked-mode
ENTRYPOINT ["dotnet", "test", "tests/Alinea.Api.Tests/Alinea.Api.Tests.csproj", "--no-restore", "--logger", "console;verbosity=normal"]

FROM source AS build
RUN dotnet restore backend/Alinea.Api/Alinea.Api.csproj --locked-mode
RUN dotnet publish backend/Alinea.Api/Alinea.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e AS runtime
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Alinea.Api.dll"]
