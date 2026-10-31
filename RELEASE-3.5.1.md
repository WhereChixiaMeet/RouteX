# RouteX 3.5.1 — automatic transition to RouteX.exe

Update normally, or run the downloaded executable. A NetProbe.exe launcher now stages and starts RouteX.exe automatically in the existing installation directory. If downloaded elsewhere, the app can discover a single installation through its running process or existing autorun task. An explicitly configured folder takes priority; ambiguous installations are not merged.

- Keeps the existing config, cores, Include/Exclude, RU bypass, TUN stack, themes, accent, layout and logs. Does not rename netprobe-vpn.exe, netprobe-direct.exe or NetProbe TUN.
- Uses a separate helper, SHA-256 checks, backups of the previous launchers and configuration, and a startup acknowledgement. A failed/timeout startup restores the previous executable and configuration and starts the fallback without a migration loop.
- Migrates owned autorun tasks to a stable per-folder RouteX task. Updates matching Desktop/Start Menu/taskbar shortcut targets and icons; preserves shortcut arguments and backs up changed links.
- Removes the old launcher only after Windows integration succeeds. If permissions or shortcut/task errors prevent completion, RouteX runs and the legacy launcher remains available. Backups and migration logs are retained in config/runtime/name-migration.
- Existing update handoff remains in use; adopted engines reapply configuration automatically instead of asking for another manual restart. Manual replacement of an already running older app may reconnect its tunnel once.
- The transition release intentionally includes **RouteX.exe and NetProbe.exe with identical bytes**, including both in the ZIP so older ZIP updaters can find RouteX.exe. Users do not need to download or delete both manually. Subsequent automatic releases publish RouteX.exe only.

Validation: 62 migration assertions plus four subprocess-chain checks, 30 UI/update checks and 18 TUN/routing checks. Includes corrupt/locked payloads, startup crashes, timeouts, invalid acknowledgement, configuration/handoff rollback, external-download installation and migration-loop prevention. Windows Task Scheduler parsed the generated XML without registering a task; a real temporary Windows shortcut was migrated. Tests did not modify live VPN settings, real autorun tasks or user shortcuts. Live VPN handoff and a Windows sign-in restart were not exercised end-to-end in this validation.

If an installation is closed, has no discoverable autorun task and the download is outside its folder, it cannot be located reliably; place/run the executable in that installation folder. A first install without existing data starts in the downloaded folder.
