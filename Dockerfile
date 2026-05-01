# syntax=docker/dockerfile:1.10
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /app

COPY vela.sln ./
COPY nuget.config ./
COPY src/Vela/Vela.csproj src/Vela/
COPY src/Vela.Contracts/Vela.Contracts.csproj src/Vela.Contracts/
COPY src/Vela.Gen/Vela.Gen.csproj src/Vela.Gen/
COPY src/Vela.AppHost/Vela.AppHost.csproj src/Vela.AppHost/
COPY src/SpacetimeDB.ClientSDK/SpacetimeDB.ClientSDK.csproj src/SpacetimeDB.ClientSDK/
COPY src/SpacetimeDB.ClientSDK/Directory.Build.props src/SpacetimeDB.ClientSDK/
RUN --mount=type=secret,id=nuget_token,env=NUGET_AUTH_TOKEN \
    dotnet restore src/Vela/Vela.csproj

COPY . .
WORKDIR /app/src/Vela
RUN dotnet publish -c Release -o /app/out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app
COPY --from=build /app/out ./

ENTRYPOINT ["dotnet", "Vela.dll"]
