FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ArkWallet.sln ./
COPY ArkWallet/ArkWallet.csproj ArkWallet/
COPY ArkWallet.Tests/ArkWallet.Tests.csproj ArkWallet.Tests/
COPY version.json ./
RUN dotnet restore

COPY . .
RUN dotnet publish ArkWallet/ArkWallet.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

RUN useradd -r -s /bin/false appuser
RUN chown -R appuser:appuser /app
USER appuser

EXPOSE 5000

ENTRYPOINT ["dotnet", "ArkWallet.dll"]
