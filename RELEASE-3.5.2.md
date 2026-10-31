# RouteX 3.5.2 — RouteX.Checker and inline routing lock

- Displays the built-in Include entry as **RouteX.Checker**, with the application icon and its built-in tag. This is a display alias: netprobe-vpn.exe and the underlying routing rules are unchanged.
- Fixes corrupted app-icon rendering for PNG-compressed ICO images by decoding the original PNG frame directly. The standard artwork is embedded for the default icon, and custom image/ICO choices remain supported.
- Shows a lock icon and **“TUN settings are locked. Turn off VPN to edit.”** above the Routing lists while the existing VPN/connection guard is active.
- Disables add/paste/delete and incoming drag-and-drop while locked, retaining readable/selectable lists and copying. Editing becomes available again after disconnect. The existing backend guards remain in place.
- Preserves the existing themes, Accent color, layout, RU bypass, mixed/gvisor selection and automatic executable migration.
- As planned after the transition release, the only published executable is **RouteX.exe**. Users of the transition version can update normally.

Validation: Windows/.NET Framework build, 43 UI/icon/update checks and 18 TUN/routing checks. UI checks include exact PNG-frame comparison, checker identity preservation and lock/unlock behavior. Tests did not start a VPN or modify live settings.
