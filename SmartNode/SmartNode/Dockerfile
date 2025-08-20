# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SmartNode/SmartNode.csproj", "SmartNode/"]
RUN dotnet restore "./SmartNode/SmartNode.csproj"
COPY . .
WORKDIR "/src/SmartNode"
RUN dotnet build "./SmartNode.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SmartNode.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=build --chown=$APP_UID /src /src
USER root
RUN apt-get update ; apt-get install -y ca-certificates curl gnupg ; curl -fsSL http://build.openmodelica.org/apt/openmodelica.asc | \
     gpg --dearmor -o /usr/share/keyrings/openmodelica-keyring.gpg ; \
     echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/openmodelica-keyring.gpg] \
  https://build.openmodelica.org/apt \
  $(cat /etc/os-release | grep "\(UBUNTU\\|DEBIAN\\|VERSION\)_CODENAME" | sort | cut -d= -f 2 | head -1) \
  stable" | tee /etc/apt/sources.list.d/openmodelica.list ; \
     apt update ; apt install -y --no-install-recommends omc
USER $APP_UID
ENTRYPOINT ["dotnet", "SmartNode.dll"]
