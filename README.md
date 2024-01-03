# SteamDSStub

My personal experiments with SteamKit2 and Steam dedicated servers.
Implements a stub Steam dedicated server that registers itself
with the master server. Simple A2S server is also provided. Does not provide any further functionality as of now.

## Functionality

- Registers a dummy Steam dedicated server with the Steam master server(s) with server info such as A2S
  data (A2S_INFO, A2S_RULES, A2S_PLAYERS) specified in a TOML file.
- A2S TCP server built on top of NetCoreServer to respond to A2S requests.
- Does not accept any actual game client connections.

Currently using my custom fork of [SteamKit2](https://github.com/tuokri/SteamKit).
