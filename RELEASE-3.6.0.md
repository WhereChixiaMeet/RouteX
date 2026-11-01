# RouteX 3.6.0

- Routing: one connection panel for source, primary profile, connection mode and backup VPN; connection options expand below it. Include / Strict VPN / Exclude use a simpler layout. Route remains beside DPI and VPN.
- Subscriptions: compact server table, full-width list and an Autoswitch button for the existing settings panel. All columns restores the complete table; sorting, testing, imports and server actions remain available.
- DPI: search and a visible strategy count, with the complete strategy list, zapret files, site lists and game settings retained.
- Fonts: four compact rows with family, size and a live preview. Existing font settings and limits are retained.
- Monitor: smoother time-based scrolling without changing measured samples, history or polling frequency. Graphs use the available width.
- Sidebar: VPN ping, Direct ping, Download and Upload can be hidden individually using the gear menu. Disabling the speed monitor still hides speed everywhere.
- Desktop widgets now display RouteX. RU sites direct and TUN stack remain in Settings → Engine; personal themes and Character settings remain available.

This release changes presentation. The routing, DNS, protocol configuration and authentication logic are unchanged. Existing installation data is retained.

## Downloads

- `RouteX.exe`: updated Windows application.
- `RouteX-3.6.0.zip`: application, source and documentation. Runtime cores and personal configuration are not bundled.

The README includes two screenshots made with a separate preview profile and synthetic data.

## Validation

Native UI checks cover all ten pages, five themes, Character visibility, editing locks, compact/full server tables, DPI search and sidebar visibility. Existing power-slider/update compatibility and TUN/RU-bypass checks also pass. UI previews do not start a VPN; no claim of a live connection test is made.
