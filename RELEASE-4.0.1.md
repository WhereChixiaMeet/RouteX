# RouteX 4.0.1 — Antivirus false-positive fix

A compatibility release: no functional changes, same features as 4.0.

- WebView2 assemblies (`Microsoft.Web.WebView2.Core.dll`, `Microsoft.Web.WebView2.WinForms.dll`) and the loader (`webview2\x64` / `webview2\x86`) now ship side-by-side with the executable instead of being unpacked and loaded from memory at runtime. Behavioral antivirus heuristics (including Windows Defender `Behavior:Win32/Persistence.A!ml`) treated the in-memory loading as dropper-like activity and blocked the fresh 4.0 build.
- The portable single-file layout still works: if the side-by-side files are absent, the app falls back to the built-in copies exactly as 4.0 did.
- Minor interface and monitoring refinements carried over from the 4.0 development line.

## Download

Fully extract **RouteX-4.0.1.zip** before running, keeping the DLL files and the `webview2` folder next to **RouteX.exe** — this layout is what keeps antivirus products from flagging the app. A standalone **RouteX.exe** asset is also provided and remains fully functional (it uses the built-in fallback), but the complete ZIP layout is the recommended and quietest option.

Windows 10/11, .NET Framework 4.8 and Microsoft Edge WebView2 Runtime are required. Keep your existing config and core folders when updating.

## Validation

Built from the 4.0.1 sources with the pinned WebView2 SDK; the release package passes the same structural checks as 4.0 (single executable in the archive, no runtime/config folders). Windows Defender on-demand scan of the packaged folder reports no threats, and the packaged build runs without triggering behavioral detections.
