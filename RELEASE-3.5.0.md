# RouteX 3.5.0 — animated DPI / VPN sliders

- Replaced DPI and VPN header buttons with the approved Slide Power design: softly rounded rectangular body and thumb, a sliding transition, and a connection spinner driven by backend state.
- Preserved the existing 150 × 36 logical size, positions, spacing and click actions on all nine pages that already have these controls.
- Colors follow the selected theme and Appearance → Accent color. Supports Dark, Light, Classic, Chisa and Kurumi; respects the Windows client-area animation preference.
- Kept standard Include/Exclude routing, RU bypass and mixed / gvisor selection. Rule Editor remains removed.
- The release contains a single executable, **NetProbe.exe**. The ZIP contains the same executable and source files; there is no second RouteX.exe.
- Updated the app updater to recognize NetProbe.exe while retaining compatibility with older RouteX.exe release names.

**Installation:** download NetProbe.exe manually for this transition release, fully close the old app and replace its executable while keeping the config and core folders. Older app versions only recognize RouteX.exe and cannot automatically install this renamed asset.

Validation: .NET Framework 4.8 build; 30 UI/update checks across nine header pages, five themes and four accents; 18 TUN/routing checks including preservation of RU bypass. No live VPN configuration was modified during tests.
