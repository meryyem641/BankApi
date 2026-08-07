FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY BankApi.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish BankApi.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "BankApi.dll"]
