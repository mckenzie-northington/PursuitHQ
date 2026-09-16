# Build and run the PursuitHQ API as a container.
#
# Render (and most hosts) build this with the API folder as the context, so
# every path below is relative to PursuitHQ.API.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The project file alone first, so a change to source code does not throw away
# the restored packages. Docker caches each step, and restore is the slow one.
COPY PursuitHQ.API.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# tzdata explicitly, and the Debian image rather than Alpine.
#
# PursuitHQ stores wall-clock times and converts them with IANA zone ids, which
# needs both ICU and the zone database. Alpine ships without ICU, and
# InvariantGlobalization would drop the zone data entirely - either one turns
# every reminder, due date and class time into the wrong hour, silently, with no
# error anywhere. Do not "optimise" this image without testing a time zone.
RUN apt-get update \
 && apt-get install -y --no-install-recommends tzdata \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# The host terminates TLS at its edge and forwards plain HTTP here, which is
# what the forwarded-headers middleware in Program.cs exists to understand.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PursuitHQ.API.dll"]
