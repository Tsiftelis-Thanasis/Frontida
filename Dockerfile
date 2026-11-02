# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore — separate layer so it's cached unless .csproj changes
COPY frontida4all.Web/frontida4all.Web.csproj frontida4all.Web/
RUN dotnet restore frontida4all.Web/frontida4all.Web.csproj

# Copy source and publish
COPY frontida4all.Web/ frontida4all.Web/
WORKDIR /src/frontida4all.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Npgsql probes for GSS encryption support during connection setup (even when
# not using Kerberos auth) and needs libgssapi_krb5, which the slim runtime
# image doesn't include by default.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Ensure upload directory exists and is writable
RUN mkdir -p wwwroot/uploads/profiles

# Run as non-root — a fixed numeric UID/GID works on every base image variant
# (some ASP.NET runtime image flavors don't include adduser/useradd at all).
RUN chown -R 1000:1000 /app
USER 1000:1000

EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=8080

ENTRYPOINT dotnet frontida4all.Web.dll --urls=http://+:$PORT
