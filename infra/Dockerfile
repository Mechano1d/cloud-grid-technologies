
# ==========================================
# BUILD STAGE
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy the complete backend source.
# This includes all modules and project references.
COPY backend/ ./backend/

# Restore dependencies for the API project.
RUN dotnet restore \
    backend/src/Monolith/OnlineBanking.Api/OnlineBanking.Api.csproj

# Publish the API and all referenced projects.
RUN dotnet publish \
    backend/src/Monolith/OnlineBanking.Api/OnlineBanking.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


# ==========================================
# RUNTIME STAGE
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

# Render provides its own PORT environment variable.
ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "OnlineBanking.Api.dll"]
