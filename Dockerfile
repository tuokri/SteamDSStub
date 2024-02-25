FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim as build-env
ARG TARGETARCH
WORKDIR /App

COPY . ./
RUN dotnet restore -a $TARGETARCH
RUN dotnet publish A2SServer/A2SServer.csproj \
    -c Release --framework net8.0 -a $TARGETARCH --no-restore -o publish/A2SServer
RUN dotnet publish DedicatedServer/DedicatedServer.csproj \
    -c Release --framework net8.0 -a $TARGETARCH --no-restore -o publish/DedicatedServer
# RUN dotnet publish SteamKit/SteamKit2/SteamKit2/SteamKit2.csproj \
#     -c Release --framework net8.0 -a $TARGETARCH --no-restore -o /publish/SteamKit2
# RUN dotnet publish NetCoreServer/source/NetCoreServer/NetCoreServer.csproj \
#     -c Release --framework net8.0 -a $TARGETARCH --no-restore -o /publish/NetCoreServer

FROM mcr.microsoft.com/dotnet/runtime:8.0-bookworm-slim

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    dos2unix \
    procps \
    python-is-python3 \
    socat \
    tar \
    tmux \
    unzip \
    wget \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

ADD network.conf /etc/sysctl.d/network.conf
RUN echo "net.ipv4.ip_forward=1" > /etc/sysctl.d/local.conf

ARG OVERMIND_VERSION="v2.4.0"
ARG OVERMIND_URL="https://github.com/DarthSim/overmind/releases/download/${OVERMIND_VERSION}/overmind-${OVERMIND_VERSION}-linux-amd64.gz"
ARG OVERMIND_SHA256="1f7cac289b550a71bebf4a29139e58831b39003d9831be59eed3e39a9097311c"
ADD ${OVERMIND_URL} ./
RUN echo "${OVERMIND_SHA256} ./overmind-${OVERMIND_VERSION}-linux-amd64.gz" \
    | sha256sum --check --status
RUN gzip -fd ./overmind-${OVERMIND_VERSION}-linux-amd64.gz
RUN mv ./overmind-${OVERMIND_VERSION}-linux-amd64 ./overmind
RUN chmod +x ./overmind
RUN mv ./overmind /usr/local/bin/

WORKDIR /App

COPY run_servers.sh run_servers.sh
COPY Procfile Procfile
COPY ds_config_1.toml ds_config_1.toml
COPY ds_config_2.toml ds_config_2.toml
COPY socat_wrapper.py socat_wrapper.py

RUN dos2unix ds_config_1.toml
RUN dos2unix ds_config_2.toml

# Can also simply use host network on Linux for these.
# ARG GAMEPORT1=8888
# ARG GAMEPORT2=8999
# ARG QUERYPORT1=29015
# ARG QUERYPORT2=37015
# EXPOSE $GAMEPORT1
# EXPOSE $GAMEPORT2
# EXPOSE $QUERYPORT1
# EXPOSE $QUERYPORT2

COPY --from=build-env /App/publish/A2SServer .
COPY --from=build-env /App/publish/DedicatedServer .
# COPY --from=build-env /App/publish/SteamKit2 .
# COPY --from=build-env /App/publish/NetCoreServer .

CMD ["bash", "./run_servers.sh"]
