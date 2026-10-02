# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Zonar.Api/Zonar.Api.csproj src/Zonar.Api/
RUN dotnet restore src/Zonar.Api/Zonar.Api.csproj
COPY src/ src/
RUN dotnet publish src/Zonar.Api/Zonar.Api.csproj -c Release -o /app --no-restore

# ---------- run ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# SQLite file lives in /data so it can be mounted as a volume
ENV ConnectionStrings__Zonar="Data Source=/data/zonar.db" \
    ASPNETCORE_URLS="http://+:8080"
RUN mkdir -p /data && chown app:app /data
USER app

EXPOSE 8080
ENTRYPOINT ["dotnet", "Zonar.Api.dll"]
