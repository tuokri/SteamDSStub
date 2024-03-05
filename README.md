# SteamDSStub

Implements a dummy Steam dedicated server that registers itself
with the master server. Simple A2S server is also provided.
Does not provide any actual game server functionality (gameplay logic).

## Functionality

- Registers a dummy Steam dedicated server with the Steam master server(s) with server info such as
  A2S data (A2S_INFO, A2S_RULES, A2S_PLAYERS) specified in a TOML file.
- A2S server built with SuperSocket to respond to A2S queries.

Currently using my custom fork of [SteamKit2](https://github.com/tuokri/SteamKit).
