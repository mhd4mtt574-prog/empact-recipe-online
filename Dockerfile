# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EmpactRecipeOnline.csproj ./
RUN dotnet restore EmpactRecipeOnline.csproj

COPY . .
RUN dotnet publish EmpactRecipeOnline.csproj -c Release -o /app/publish

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render / Fly.io / most free hosts inject a PORT env var at runtime and
# expect the app to bind to it, so we resolve it at container start.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# The app's default data-folder resolution collides with the published
# apphost executable (both end up named "EmpactRecipeOnline" under /app).
# Point it at a dedicated writable folder instead, using the app's own
# EMPACT_RECIPE_DATA_FOLDER override.
ENV EMPACT_RECIPE_DATA_FOLDER=/app/data
RUN mkdir -p /app/data

ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet EmpactRecipeOnline.dll"]
