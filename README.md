# RouteX
*(formerly NetProbe)*

**4.0 UI preview (not released):** redesigned pages, fixed layouts, settings backup, persistent palettes, and clearer probe monitoring. The latest published application is still [RouteX 3.6.0](https://github.com/WhereChixiaMeet/RouteX/releases/tag/v3.6.0).

### Interface preview

Upcoming 4.0 UI, captured in an isolated demo profile. Connection states and measurements are simulated; names and keys are sample data. Any displayed IP addresses are reserved documentation examples. Click an image for the full-size view.

| Routing | Subscriptions |
|:---:|:---:|
| <a href="docs/images/routex-4.0-routing.png"><img src="docs/images/routex-4.0-routing.png" alt="Routing preview" width="430"></a> | <a href="docs/images/routex-4.0-subscriptions.png"><img src="docs/images/routex-4.0-subscriptions.png" alt="Subscriptions preview" width="430"></a> |
| **Monitor** | **DPI configs** |
| <a href="docs/images/routex-4.0-monitor.png"><img src="docs/images/routex-4.0-monitor.png" alt="HTTP latency monitor preview" width="430"></a> | <a href="docs/images/routex-4.0-dpi.png"><img src="docs/images/routex-4.0-dpi.png" alt="DPI strategy editor preview" width="430"></a> |
| **App settings** | **Appearance** |
| <a href="docs/images/routex-4.0-app.png"><img src="docs/images/routex-4.0-app.png" alt="App startup and settings backup preview" width="430"></a> | <a href="docs/images/routex-4.0-appearance.png"><img src="docs/images/routex-4.0-appearance.png" alt="Themes and custom palette preview" width="430"></a> |

3.5.2: the built-in VPN probe is displayed as **RouteX.Checker** with the app icon; its internal process identity remains unchanged. Routing lists show an inline lock explanation and disabled editing controls while the VPN is active. This release publishes only **RouteX.exe**.

**Settings → Engine → TUN stack** selects `mixed` or `gvisor` for Configs/subscription TUN. Disconnect before changing and reconnect to apply. Existing installs keep `mixed`.

3.5.1 automatically migrates the main executable to **RouteX.exe** in the existing installation directory. The transition release includes an identical NetProbe.exe bridge for older updaters; both enter the same migration. Data folders and internal network identities remain unchanged. The old launcher is backed up and removed after startup and Windows integration succeed; failed integration retains it. See [migration notes](RELEASE-3.5.1.md).

Version 3.4.10: update checks fall back to github.com pages when the GitHub API answers 403 (shared VPN IP rate limit). See [release notes](docs/releases/v3.4.10.md).

### MANUAL RESTART REQUIRED AFTER UPDATE ###

Windows protocol router: WireGuard · VLESS · VMess · Trojan, DPI bypass, MTProxy (TG) per-app split routing with automatic failover and a built-in network monitor.

## Requirements
- Windows 10/11 and .NET Framework 4.8. The upcoming 4.0 UI also requires Microsoft Edge WebView2 Runtime.
- Runs as administrator (WinDivert, TUN and the firewall kill switch need it).
- WireGuard tunnels use the WireSock VPN Client 1.4.x engine. VLESS / VMess / Trojan tunnels and subscriptions use xray-core + sing-box (TUN), both installable from inside the app.

## Folder layout
```
RouteX.exe       (main application)
zapret\          winws.exe, WinDivert, lists (copied from zapret-discord-youtube)
xray\, singbox\  cores, installed/updated from the app
config\          settings.ini, tunnels\*.conf|*.json, dpi\*.txt, lists\*.txt, themes\, runtime\
log\             one file per day: YYYY-MM-DD.log (folder can be changed in Settings)
src\             sources, build.cmd
```

## Routing modes (segment ROUTE | VPN | DPI)
- **ROUTE** (DPI/VPN) — Include apps go through the tunnel, DPI handles everything outside it at the same time. If the tunnel probe gets no answer for N seconds: primary → backup → DPI failover; back to the tunnel once it is stable again.
- **VPN** — tunnel only (primary → backup). On outage the kill switch blocks Strict VPN apps.
- **DPI** — winws only, no tunnel.

Clicking the active chip disconnects; clicking another chip switches mode and connects.

## Protocols
The protocol is a property of each tunnel config (Configs tab): **WireGuard** (via WireSock), **VLESS**, **VMess**, **Trojan** (via xray + sing-box TUN). Every protocol works the same way on top: same router states, lists, kill switch, DPI failover and recovery. Links (`vless://`, `vmess://`, `trojan://`) fill the fields automatically.

## Sources
- At least one tunnel → Primary VPN is the source, subscriptions are off.
- No tunnels → subscriptions (xray) are the source.
- **Hybrid mode** (Settings) → both, with a manual "Primary VPN ⇄ Subscriptions" switch. Primary VPN always has priority.

## Lists (Routing tab — apps only)
- **Include** — apps in the tunnel (empty = all traffic).
- **Strict VPN** — always in the tunnel, never DPI; blocked by the kill switch when the tunnel is down.
- **Exclude** — bypass the tunnel.
Disconnect the VPN before adding, deleting, pasting, renaming or moving entries in TUN routing lists. Changes take effect on the next connection.
DPI is global: domains for DPI and "don't touch these sites" live on Configs → DPI → Sites. Priority: Strict VPN > domain > app.

## Build
```
src\build.cmd
```
Uses Roslyn from the installed .NET SDK, targets .NET Framework 4.8.

## Languages
English · Español · 中文 (Settings → Language, applied without restart).

## Known limits
- winws cannot filter by process; legacy process exclusions from 2.x work through current TCP connections (QUIC/UDP not covered).
- WireSock cannot run twice: stop `wiresock-client-service` (TunnlTo) first — RouteX offers to do it.

## Known Issues
- the application breaks the routing configuration during an update (SAME ROUTE issue)
- minor UI issues using custom themes
