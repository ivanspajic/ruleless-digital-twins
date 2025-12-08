# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/devcontainers/dotnet:9.0
COPY --from=mcr.microsoft.com/dotnet/sdk:8.0 /usr/share/dotnet /usr/share/dotnet
# [VS] We keep `omc` in the final product. If we only want the compiled FMU, and never expect the user to invoke `omc`
#       in the container themselves, we could move this bit up a layer or two.
USER root
RUN apt-get update ; apt-get install -y ca-certificates curl gnupg file gdb default-jre-headless python3-venv python3-pip ; curl -fsSL http://build.openmodelica.org/apt/openmodelica.asc | \
     gpg --dearmor -o /usr/share/keyrings/openmodelica-keyring.gpg ; \
     echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/openmodelica-keyring.gpg] \
  https://build.openmodelica.org/apt \
  $(cat /etc/os-release | grep "\(UBUNTU\\|DEBIAN\\|VERSION\)_CODENAME" | sort | cut -d= -f 2 | head -1) \
  nightly" | tee /etc/apt/sources.list.d/openmodelica.list ; \
     apt update ; apt install -y --no-install-recommends omc ; apt clean
# [VS] We have a `pip install --user ...` in devcontainer.json as well as i) I don't want to spam /usr, ii) Ivan needs those for `root` whereas I for `vscode`.
#      We use a named volume to keep some Arm-specific stuff around that I otherwise have to rebuild from source every time (Ohi `cosim`!):
RUN mkdir -p /opt/arm64 && chown vscode:vscode /opt/arm64
# [VS] We want this for Python FMUs until we find out how to set if for execution or test-case invocations. Oh FFS...
# Docker spells it arm64, but inside it's aarch64, and there's not translation mechanism out here or in devcontainer.json.
ARG TARGETARCH
RUN if [ "${TARGETARCH}" = "arm64" ]; then \
      ln -s /usr/lib/aarch64-linux-gnu /usr/lib/${TARGETARCH}-linux-gnu ; fi
ENV LD_PRELOAD=/usr/lib/${TARGETARCH}-linux-gnu/libpython3.11.so