FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["gdb-service-dotnet.slnx", "./"]
COPY ["Directory.Build.props", "Directory.Packages.props", "global.json", "./"]
COPY ["NotificationService/NotificationService.csproj", "NotificationService/"]
COPY ["shared/Gdb.Common/Gdb.Common.csproj", "shared/Gdb.Common/"]
RUN dotnet restore "NotificationService/NotificationService.csproj"
COPY . .
WORKDIR "/src/NotificationService"
RUN dotnet publish "NotificationService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD curl -fsS http://localhost:8007/live || exit 1
COPY --from=build /app/publish .
# Run as the image's built-in non-root user; make /app writable (e.g. SQLite db file).
RUN chown -R app:app /app
USER app
ENV ASPNETCORE_URLS=http://+:8007
EXPOSE 8007
ENTRYPOINT ["dotnet", "NotificationService.dll"]
