# RouteX 4.0 — Redesigned interface

A complete interface redesign for secure connections, per-app routing, subscriptions and connection monitoring.

- Rebuilt Routing, Subscriptions, Monitor, Tunnel Configs, DPI configs, Tests, Sites, Log & polling, and Settings.
- Theme-aware Tunnel and DPI switches, accent-colored list selection, hover states, drag-and-drop, and per-entry context menus.
- Primary config / Backup config controls. Hybrid mode asks which source to use when both are available; with Hybrid off, only Primary config can connect. The restriction explanation is shown once per installation, including across future updates.
- Full subscription server actions: connect, latency and speed tests, favorites, copy link, reset traffic and delete confirmation. Autoswitch controls and Total traffic remain available.
- Fixed interface layout, with background initialization for minimized startup and a smooth loading transition. The 4.0 welcome appears once after updating.
- Dedicated App settings for Windows startup, widgets and sidebar indicators. Custom palettes persist across theme changes and include Reset.
- Settings import/export restored, preserving the legacy backup format. New exports include both Character themes, all image slots and their saved settings.
- Configurable DPI source folder. Updates use the selected destination; an unset path defaults to the application's zapret folder.
- RU direct routing, IPv4-only TUN, DNS settings, mixed/gvisor selection, configuration editing, and existing data folders retained.
- Probe failures are distinguished from connection loss. A stable normal endpoint and passive checks reduce unnecessary requests; polling is capped at 0.7 polls/s per channel.

## Download

Use **RouteX.exe**, or fully extract **RouteX-4.0.zip** before running it. The ZIP includes the executable, sources and license notices, without personal settings or logs. Keep your existing config and core folders when updating.

Windows 10/11, .NET Framework 4.8 and Microsoft Edge WebView2 Runtime are required. The canonical executable is RouteX.exe; the installed connection and DPI cores retain their existing folders.

## Validation

Production UI/backend regression tests, real pointer hit-testing, 18 offline TUN/RU-routing checks, legacy/Character backup compatibility checks, and 62 migration/rollback checks passed. The migration subprocess chain, external-download handoff, background startup and welcome dismissal were checked separately. Startup appearance was captured from actual rendered frames. These checks do not constitute a live connection throughput test or a complete security audit.
