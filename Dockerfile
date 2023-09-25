FROM mcr.microsoft.com/dotnet/sdk:7.0-bookworm-slim as build-env
WORKDIR /App

COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release --framework net7.0

FROM mcr.microsoft.com/dotnet/runtime:7.0-bookworm-slim

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
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
# TODO: checksum not supported by fly.io. Running old version?
# --checksum=sha256:1f7cac289b550a71bebf4a29139e58831b39003d9831be59eed3e39a9097311c \
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

# Using host network for these.
# ARG GAMEPORT
# ARG QUERYPORT
# EXPOSE $GAMEPORT/udp
# EXPOSE $QUERYPORT/udp

COPY --from=build-env /App/SteamKit/SteamKit2/SteamKit2/bin/Release/net7.0/ .
COPY --from=build-env /App/DedicatedServer/bin/Release/net7.0/ .
COPY --from=build-env /App/A2SServer/bin/Release/net7.0/ .

CMD ["bash", "./run_servers.sh"]
