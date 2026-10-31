# RouteX 3.4.12 — TUN stack selection

- Added **Settings → Engine → TUN stack** with `mixed` and `gvisor` choices.
- The selection is saved and applies on the next connection to sing-box TUN in Configs and subscriptions. Disconnect VPN before changing it.
- The default remains `mixed`. WireGuard is unaffected.
- `gvisor` requires a sing-box build with gVisor support. The official Windows amd64 sing-box 1.14.4 build supports it.

This release adds a comparison option; it does not claim to fix YouTube interruptions or guarantee higher speed.

Validation: .NET Framework 4.8 build and 14 offline checks passed. Generated Configs and subscription configurations for both stacks were checked with official sing-box 1.14.4. Live VPN settings were not changed during validation.

Download RouteX.exe for an existing installation, or the ZIP for the executable and source files. NetProbe.exe is an identical compatibility alias. Keep your existing config and core folders.
