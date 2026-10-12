# Test identity provider for ReadyStackGo's browser tests. Never part of the product image.
# Build from the repository root:
#   docker build -f tests/ReadyStackGo.TestIdentityProvider/Dockerfile -t rsgo-test-idp .
# Settings (environment): Issuer (default http://localhost:9090/),
# Clients__0__ClientId, Clients__0__ClientSecret, Clients__0__RedirectUris__0, ...

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY global.json ./
COPY tests/ReadyStackGo.TestIdentityProvider/ReadyStackGo.TestIdentityProvider.csproj ./tests/ReadyStackGo.TestIdentityProvider/
RUN dotnet restore tests/ReadyStackGo.TestIdentityProvider/ReadyStackGo.TestIdentityProvider.csproj

COPY tests/ReadyStackGo.TestIdentityProvider/ ./tests/ReadyStackGo.TestIdentityProvider/
RUN dotnet publish tests/ReadyStackGo.TestIdentityProvider/ReadyStackGo.TestIdentityProvider.csproj \
    --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_HTTP_PORTS=9090
EXPOSE 9090

ENTRYPOINT ["dotnet", "ReadyStackGo.TestIdentityProvider.dll"]
