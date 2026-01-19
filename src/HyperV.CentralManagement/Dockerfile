# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/HyperV.CentralManagement/HyperV.CentralManagement.csproj", "src/HyperV.CentralManagement/"]
RUN dotnet restore "src/HyperV.CentralManagement/HyperV.CentralManagement.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/HyperV.CentralManagement"
RUN dotnet build "HyperV.CentralManagement.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "HyperV.CentralManagement.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Create non-root user for security
RUN groupadd -r hyperv && useradd -r -g hyperv hyperv

# Create directories for logs and data
RUN mkdir -p /var/log/hyperv-central && \
    chown -R hyperv:hyperv /var/log/hyperv-central

# Copy published app
COPY --from=publish /app/publish .

# Change ownership
RUN chown -R hyperv:hyperv /app

# Switch to non-root user
USER hyperv

# Expose ports
EXPOSE 80 443

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl -f http://localhost/health || exit 1

# Set environment
ENV ASPNETCORE_URLS=http://+:80 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ENTRYPOINT ["dotnet", "HyperV.CentralManagement.dll"]
