# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
# [VS] We keep `omc` in the final product. If we only want the compiled FMU, and never expect the user to invoke `omc`
#       in the container themselves, we could move this bit up a layer or two.
USER root
RUN apt-get update ; apt-get install -y ca-certificates curl gnupg default-jre-headless; curl -fsSL http://build.openmodelica.org/apt/openmodelica-2026.asc | \
     gpg --dearmor -o /usr/share/keyrings/openmodelica-keyring.gpg ; \
     echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/openmodelica-keyring.gpg] \
  https://build.openmodelica.org/apt \
  $(cat /etc/os-release | grep "\(UBUNTU\\|DEBIAN\\|VERSION\)_CODENAME" | sort | cut -d= -f 2 | head -1) \
  stable" | tee /etc/apt/sources.list.d/openmodelica.list ; \
     apt update ; apt install -y --no-install-recommends omc ; apt clean
USER $APP_UID
WORKDIR /app

FROM base AS fmus
COPY Implementations/ .
RUN cd FMUs/Source && make all
RUN pwd && ls -l FMUs/Source

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-deps
WORKDIR /src
COPY SmartNode-release.sln */*.csproj ./
RUN for i in Logic SmartNode Implementations; do mkdir -p /src/$i && mv $i.csproj /src/$i/; done
# Subpath :-/
RUN mkdir -p /src/Femyou/src
COPY Femyou/src/Femyou.csproj /src/Femyou/src/
# There's something fishy about the NuGet Git
RUN dotnet restore "./SmartNode-release.sln"

FROM build-deps AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet build "./SmartNode-release.sln" --no-restore -c $BUILD_CONFIGURATION /p:UseAppHost=false

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# PublishDir will spew all dll into a single folder (not per-project), overwriting dependencies where necessary #-}
RUN dotnet publish "./SmartNode-release.sln" --no-build -o /publish/ -c $BUILD_CONFIGURATION /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
# Don't ship Windows FMUs -- not to save space but to avoid confusion:
COPY --from=publish --chown=$APP_UID --exclude=**/*.fmu /publish .
COPY --from=fmus --chown=$APP_UID /app/FMUs/Source/roomM370.fmu /app/FMUs/Source/au_incubator.fmu /app/
RUN mkdir /app/models
ENTRYPOINT ["dotnet", "SmartNode.dll"]
