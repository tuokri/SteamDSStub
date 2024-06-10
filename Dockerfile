FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim as build-env
ARG TARGETARCH
WORKDIR /App

COPY . ./
RUN dotnet restore -a $TARGETARCH
RUN dotnet publish A2SServer/A2SServer.csproj \
    -c Release --framework net8.0 -a $TARGETARCH --no-restore -o publish/A2SServer
RUN dotnet publish DedicatedServer/DedicatedServer.csproj \
    -c Release --framework net8.0 -a $TARGETARCH --no-restore -o publish/DedicatedServer

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

ARG OVERMIND_VERSION="v2.5.1"
ARG OVERMIND_URL="https://github.com/DarthSim/overmind/releases/download/${OVERMIND_VERSION}/overmind-${OVERMIND_VERSION}-linux-amd64.gz"
ARG OVERMIND_SHA256="a17159b8e97d13f3679a4e8fbc9d4747f82d5af9f6d32597b72821378b5d0b6f"
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

# TODO:
# Instead of hard-coding destination server in a config file,
# have a master "orchestrator" that sets up all destinations
# for multiple servers to "legit" servers.

COPY --from=build-env /App/publish/A2SServer .
COPY --from=build-env /App/publish/DedicatedServer .

CMD ["bash", "./run_servers.sh"]
