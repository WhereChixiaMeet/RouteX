# RouteX 4.0.9 - Routing row spacing and TG Proxy restart

- Added **Settings > Appearance > Application routing > Row spacing**. The default preserves the existing layout; reduce spacing to fit more Include / Strict / Exclude entries. The setting persists across themes and restarts.
- Fixed TG Proxy Restart races that could leave the local listener behind and cause a port-in-use error until the application was restarted. Stop/start operations are serialized, stale queued starts are ignored, and old connection handlers cannot replace the new listener. Restart releases RouteX's own socket; it cannot release a port owned by another program.

Built directly from v4.0.8. Existing interface, configuration, connection engines and settings are preserved apart from these two changes. No other interface redesign or connection-routing changes are included.

Validation: UI/backend regression tests passed, including compact/default spacing, persistence and theme changes. Loopback-only tests covered same-port restart, closing old sessions, bind-error recovery, concurrent restart requests, interleaved Start/Stop and retaining foreign listeners. These tests do not constitute a live Telegram connectivity test.

Fully extract **RouteX-4.0.9.zip**, close RouteX and replace the application files while keeping existing config and core folders. Keep the included WebView2 libraries beside RouteX.exe. Requires .NET Framework 4.8 and Microsoft Edge WebView2 Runtime.
