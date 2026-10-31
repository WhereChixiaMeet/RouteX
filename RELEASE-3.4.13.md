# RouteX 3.4.13 — standard routing, RU bypass retained

- Removed the Rule Editor UI and rule-set routing engine. Configs use the standard Include/Exclude lists again.
- Old editor flags and routing.json no longer replace the normal lists, even if the editor was enabled in a previous version.
- **RU bypass remains available** with the standard Russian-domain and geoip:ru Direct rules.
- Kept the mixed / gvisor TUN stack selector in Settings → Engine, IPv4-only TUN, DNS settings and the existing connection fixes.

Validation: .NET Framework 4.8 build and 18 offline checks passed, including migration from enabled editor settings, preservation of RU bypass in Configs, generated Xray RU domain/IP rules, and both TUN stacks. Generated sing-box configurations were checked with official sing-box 1.14.4. Live VPN settings were not changed.

Download RouteX.exe for an existing installation, or the ZIP for executables and source files. NetProbe.exe is an identical compatibility alias. Keep your existing config and core folders.
